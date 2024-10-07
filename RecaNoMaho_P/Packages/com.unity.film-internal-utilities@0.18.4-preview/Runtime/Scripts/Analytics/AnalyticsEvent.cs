using System;

namespace Unity.FilmInternalUtilities {
internal class AnalyticsEventData {
    public string actualPackageVersion;
}

internal abstract class AnalyticsEvent {
    
    internal abstract string eventName       { get; }
    internal virtual  int    version         => 1;
    internal virtual  int    maxEventPerHour => 10000;
    internal virtual  int    maxItems        => 1000;

    // Minimum interval to send this event
    internal virtual TimeSpan minInterval => TimeSpan.Zero;

    internal readonly AnalyticsEventData parameters;

    internal AnalyticsEvent() {
        parameters = new AnalyticsEventData();
    }

    internal AnalyticsEvent(AnalyticsEventData eventData) {
        parameters = eventData;
    }
}

[Obsolete("Use AnalyticsEvent instead")]
internal abstract class AnalyticsEvent<T> : AnalyticsEvent  {
    internal AnalyticsEvent() : base() {
    }

    internal AnalyticsEvent(T eventData) : base() {
    }
}

} //end namespace