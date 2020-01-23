﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersonalPortfolio.Shared.Core;
using PersonalPortfolio.Shared.Storage.Abstractions;

namespace PersonalPortfolio.Shared.Storage.Commands
{
    public class CurrencyCommandService : ICurrencyCommandService
    {
        private readonly PortfolioDbContext _ctx;
        private readonly ILogger<ICurrencyCommandService> _logger;
        private readonly IBulkCommandsService _bulkCommandsService;

        public CurrencyCommandService(
            PortfolioDbContext ctx,
            ILogger<ICurrencyCommandService> logger,
            IBulkCommandsService bulkCommandsService)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bulkCommandsService = bulkCommandsService ?? throw new ArgumentNullException(nameof(bulkCommandsService));
        }

        public async Task<int> AddOrUpdateAsync(IEnumerable<CurrencyInfo> infos, CancellationToken token)
        {

            // TODO: bulk insert or update
            var counter = new ConcurrentBag<int>();

            foreach (var currencyInfo in infos)
            {
                var data = await _ctx.Currencies
                    .FirstOrDefaultAsync(m => m.Code == currencyInfo.Code, token)
                    .ConfigureAwait(false);
                
                if (data != null)
                {
                    data.Description = currencyInfo.Description;
                    data.DateUpdated = DateTime.UtcNow;
                }
                else
                {
                    data = new Currency
                    {
                        Description = currencyInfo.Description,
                        Code = currencyInfo.Code
                    };

                    await _ctx.Currencies.AddAsync(data, token).ConfigureAwait(false);
                }

                var count = await _ctx.SaveChangesAsync(token)
                    .ConfigureAwait(false);

                counter.Add(count);
            }

            return counter.Sum();
        }

        public async Task<int> AddRatesAsync(IEnumerable<(DateTime, string, string, decimal, string)> rates, CancellationToken token)
        {
            var currencyMap = await _ctx.Currencies
                .ToDictionaryAsync(c => c.Code, v => v.Id, token)
                .ConfigureAwait(false);

            var currencyTimestamps = await _ctx.CurrencyRates
                .GroupBy(e => new { e.SourceCurrencyId, e.CurrencyId, e.DataSourceId })
                .Select(g => new { g.Key, Value = g.Max(e => e.RateTime) })
                .ToDictionaryAsync(e => e.Key, e => e.Value, token)
                .ConfigureAwait(false);

            var entities = new List<CurrencyRate>();

            foreach (var (dateTime, sourceCode, targetCode, value, dataSourceId) in rates)
            {
                if (!currencyMap.ContainsKey(sourceCode) || !currencyMap.ContainsKey(targetCode))
                {
                    _logger.LogWarning("Unknown currency pair: {0}-{1}", sourceCode, targetCode);
                    continue;
                }

                var sourceId = currencyMap[sourceCode];
                var targetId = currencyMap[targetCode];

                entities.Add(new CurrencyRate
                {
                    SourceCurrencyId = sourceId,
                    CurrencyId = targetId,
                    RateTime = dateTime.Date,
                    Value = value,
                    DataSourceId = 0 // TODO: dataSourceId
                });
            }

            int counter;

            if (_bulkCommandsService == null)
            {
                await _ctx.CurrencyRates.AddRangeAsync(entities, token)
                    .ConfigureAwait(false);

                counter = await _ctx.SaveChangesAsync(token)
                    .ConfigureAwait(false);
            }
            else
            {
                counter = await _bulkCommandsService.InsertAsync(entities, token).ConfigureAwait(false);
            }

            _logger.LogDebug("Inserted {0} items.", counter);

            return counter;
        }
    }
}
