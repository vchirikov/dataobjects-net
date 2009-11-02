// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Krjuchkov
// Created:    2009.07.02

using System;
using Xtensive.Core.Helpers;

namespace Xtensive.Sql.PostgreSql
{
  internal static class IntervalHelper
  {
    public static TimeSpan StringToTimeSpan(string input)
    {
      int days = 0;
      int hours = 0;
      int minutes = 0;
      int seconds = 0;
      int milliseconds = 0;

      //pattern: [[-]DD* day[s]] [[-]H[H]:M[M]:S[S][.ff*]]
      bool mainPartNegative;
      string[] parts = input.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
      switch (parts.Length) {
        case 1:
          // no day part
          mainPartNegative = ParseMainIntervalPart(parts[0], out hours, out minutes, out seconds, out milliseconds);
          break;
        case 2:
          // only day part: "x days"
          mainPartNegative = false;
          days = int.Parse(parts[0]);
          break;
        case 3:
          // both day and HMS parts: "x days y:z:v.ww"
          days = int.Parse(parts[0]);
          mainPartNegative = ParseMainIntervalPart(parts[2], out hours, out minutes, out seconds, out milliseconds);
          break;
        default:
          throw new InvalidOperationException();
      }

      if (mainPartNegative || days < 0) {
        hours = -hours;
        minutes = -minutes;
        seconds = -seconds;
        milliseconds = -milliseconds;
        if (days >= 0)
          days = -days;
      }

      return new TimeSpan(days, hours, minutes, seconds, milliseconds);
    }

    private static bool ParseMainIntervalPart(string value,
      out int hours,
      out int minutes,
      out int seconds,
      out int millisececonds)
    {
      hours = minutes = seconds = millisececonds = 0;
      bool negativeResult;
      value = value.TryCutPrefix("-", out negativeResult);
      string[] parts = value.Split(':', '.');
      if (parts.Length == 0)
        return false;

      hours = int.Parse(parts[0]);
      if (parts.Length > 1) {
        minutes = int.Parse(parts[1]);
        if (parts.Length > 2) {
          seconds = int.Parse(parts[2]);
          if (parts.Length > 3)
            if (parts[3].Length > 4) {
              millisececonds = int.Parse(parts[3].Substring(0, 4));
              if (millisececonds % 10 > 4)
                millisececonds += 10;
              millisececonds /= 10;
            }
            else
              millisececonds = int.Parse(parts[3].PadRight(3, '0'));
        }
      }
      
      return negativeResult;
    }

    public static string TimeSpanToString(TimeSpan value)
    {
      int days = value.Days;
      int hours = value.Hours;
      int minutes = value.Minutes;
      int seconds = value.Seconds;
      int milliseconds = value.Milliseconds;

      bool negative = hours < 0 || minutes < 0 || seconds < 0 || milliseconds < 0;

      if (hours < 0)
        hours = -hours;

      if (minutes < 0)
        minutes = -minutes;

      if (seconds < 0)
        seconds = -seconds;

      if (milliseconds < 0)
        milliseconds = -milliseconds;

      return String.Format("{0} days {1}{2}:{3}:{4}.{5:000}",
          days, negative ? "-" : "", hours, minutes, seconds, milliseconds);
    }
  }
}