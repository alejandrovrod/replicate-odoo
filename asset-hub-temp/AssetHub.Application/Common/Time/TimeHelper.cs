using System;

namespace AssetHub.Application.Common.Time;

public static class TimeHelper
{
    public static TimeZoneInfo GetMexicoCityTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Local;
            }
        }
    }
}
