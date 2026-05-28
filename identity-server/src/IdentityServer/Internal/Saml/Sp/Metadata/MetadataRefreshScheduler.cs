// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal static class MetadataRefreshScheduler
    {
        internal static TimeSpan minInterval = new TimeSpan(0, 1, 0);

        // Maximum delay supported by Task.Delay()
        private static readonly TimeSpan maxInterval = new TimeSpan(0, 0, 0, 0, int.MaxValue);

        internal static TimeSpan GetDelay(DateTime validUntil, TimeProvider timeProvider)
        {
            var timeRemaining = validUntil - timeProvider.GetUtcNow().UtcDateTime;
            var delay = new TimeSpan(timeRemaining.Ticks / 2);

            if (delay < minInterval)
            {
                return minInterval;
            }

            if (delay > maxInterval)
            {
                return maxInterval;
            }

            return delay;
        }

        public static readonly XsdDuration DefaultMetadataCacheDuration = new XsdDuration(hours: 1);

        internal static DateTime CalculateMetadataValidUntil(this ICachedMetadata metadata, TimeProvider timeProvider)
        {
            return metadata.ValidUntil ??
                   timeProvider.GetUtcNow().UtcDateTime.Add((metadata.CacheDuration ?? DefaultMetadataCacheDuration)
                    .ToTimeSpan());
        }
    }
}
