﻿using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ZBlazor
{
    public class InMemoryRecentRepository : IRecentRepository
    {
        private readonly ConcurrentDictionary<string, DateTime> _recents = new ConcurrentDictionary<string, DateTime>();
        private readonly string _repositoryName;

        public InMemoryRecentRepository(string name)
        {
            _repositoryName = name ?? throw new ArgumentNullException(nameof(name));
        }

        public ValueTask AddHit(string key)
        {
            key = GetFullKey(key);

            if (_recents.TryGetValue(key, out DateTime value))
            {
                _recents.TryUpdate(key, DateTime.Now, value);
            }
            else
            {
                _recents.TryAdd(key, DateTime.Now);
            }

            return new ValueTask();
        }

        public ValueTask<DateTime?> GetHitsForKey(string key)
        {
            if (_recents.TryGetValue(GetFullKey(key), out DateTime value))
            {
                return new ValueTask<DateTime?>(value);
            }

            return new ValueTask<DateTime?>(result: null);
        }

        private string GetFullKey(string key)
            => $"{_repositoryName}_{key}";
    }
}
