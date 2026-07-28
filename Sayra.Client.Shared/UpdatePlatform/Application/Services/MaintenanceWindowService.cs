using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Service for evaluating maintenance windows including timezones, holiday exclusions, and allowed days.
    /// </summary>
    public class MaintenanceWindowService : IMaintenanceWindowService
    {
        private readonly MaintenanceWindowOptions _options;

        public MaintenanceWindowService(IOptions<MaintenanceWindowOptions> options)
        {
            _options = options?.Value ?? new MaintenanceWindowOptions();
        }

        public MaintenanceWindow GetMaintenanceWindow()
        {
            var window = new MaintenanceWindow
            {
                StartTime = TimeSpan.Parse(_options.StartTimeUtc),
                EndTime = TimeSpan.Parse(_options.EndTimeUtc),
                TimeZoneId = _options.TimeZoneId,
                AllowForcedUpgrades = _options.AllowForcedUpgrades
            };

            foreach (var dayStr in _options.AllowedDays)
            {
                if (Enum.TryParse<DayOfWeek>(dayStr, true, out var day))
                {
                    window.AllowedDays.Add(day);
                }
            }

            foreach (var holidayStr in _options.HolidayExclusions)
            {
                if (DateTime.TryParse(holidayStr, out var holiday))
                {
                    window.HolidayExclusions.Add(holiday.Date);
                }
            }

            return window;
        }

        public bool IsInsideWindow(DateTime timeToCheck)
        {
            var window = GetMaintenanceWindow();
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(window.TimeZoneId);
            }
            catch (Exception)
            {
                tz = TimeZoneInfo.Utc;
            }

            // Convert check time to the target time zone
            DateTime localTime = TimeZoneInfo.ConvertTime(timeToCheck, tz);

            // Check if day is allowed
            if (window.AllowedDays.Count > 0 && !window.AllowedDays.Contains(localTime.DayOfWeek))
            {
                return false;
            }

            // Check if holiday
            if (window.HolidayExclusions.Contains(localTime.Date))
            {
                return false;
            }

            TimeSpan checkTime = localTime.TimeOfDay;
            if (window.StartTime <= window.EndTime)
            {
                return checkTime >= window.StartTime && checkTime <= window.EndTime;
            }
            else
            {
                // Windows that cross midnight
                return checkTime >= window.StartTime || checkTime <= window.EndTime;
            }
        }

        public DateTime GetNextWindowStart(DateTime currentTime)
        {
            var window = GetMaintenanceWindow();
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(window.TimeZoneId);
            }
            catch (Exception)
            {
                tz = TimeZoneInfo.Utc;
            }

            DateTime localTime = TimeZoneInfo.ConvertTime(currentTime, tz);

            for (int i = 0; i < 30; i++) // search up to 30 days ahead
            {
                DateTime checkDay = localTime.Date.AddDays(i);
                DateTime startCandidate = checkDay.Add(window.StartTime);

                // If candidate is in the past compared to localTime, skip
                if (startCandidate < localTime)
                {
                    continue;
                }

                // Check day of week
                if (window.AllowedDays.Count > 0 && !window.AllowedDays.Contains(checkDay.DayOfWeek))
                {
                    continue;
                }

                // Check holidays
                if (window.HolidayExclusions.Contains(checkDay.Date))
                {
                    continue;
                }

                // Found the next window start, convert back to UTC/original kind
                return TimeZoneInfo.ConvertTime(startCandidate, tz, currentTime.Kind == DateTimeKind.Utc ? TimeZoneInfo.Utc : TimeZoneInfo.Local);
            }

            return currentTime.AddDays(1); // default fallback
        }

        public void EnsureInsideWindow(DateTime timeToCheck, bool overrideForForced = false)
        {
            var window = GetMaintenanceWindow();
            if (overrideForForced && window.AllowForcedUpgrades)
            {
                return; // forced updates bypass maintenance windows
            }

            if (!IsInsideWindow(timeToCheck))
            {
                throw new MaintenanceWindowViolationException($"Current time {timeToCheck} is outside of the configured maintenance window: {window.StartTime} - {window.EndTime} on allowed days.");
            }
        }
    }
}
