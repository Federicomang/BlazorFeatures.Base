using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace BlazorFeatures.Abstractions
{
    /// <summary>
    /// Collects caller-scoped data before it is converted to an immutable snapshot.
    /// </summary>
    public sealed class FeatureCallerContextBuilder
    {
        public ClaimsPrincipal User { get; set; }

        public IDictionary<string, object> Values { get; }

        public FeatureCallerContextBuilder()
            : this(new FeatureCallerContext(
                new ClaimsPrincipal(new ClaimsIdentity())))
        {
        }

        public FeatureCallerContextBuilder(FeatureCallerContext source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            User = source.User;
            Values = new Dictionary<string, object>();
            foreach (var pair in source.Values)
                Values[pair.Key] = pair.Value;
        }

        public FeatureCallerContext Build() =>
            new(User, new Dictionary<string, object>(Values));
    }
}
