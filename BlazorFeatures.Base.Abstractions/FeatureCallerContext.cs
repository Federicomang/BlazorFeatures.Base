using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;

namespace BlazorFeatures.Abstractions
{
    /// <summary>
    /// Immutable snapshot of caller-scoped data captured before a feature scope is created.
    /// </summary>
    public sealed class FeatureCallerContext
    {
        public ClaimsPrincipal User { get; }

        public IReadOnlyDictionary<string, object> Values { get; }

        public FeatureCallerContext(
            ClaimsPrincipal user,
            IReadOnlyDictionary<string, object>? values = null)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            User = user;
            var valueSnapshot = new Dictionary<string, object>();
            if (values != null)
            {
                foreach (var pair in values)
                    valueSnapshot[pair.Key] = pair.Value;
            }
            Values = new ReadOnlyDictionary<string, object>(valueSnapshot);
        }
    }
}
