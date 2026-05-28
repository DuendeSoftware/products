// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Text;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    internal struct XsdDuration
    {
        public int Years { get; private set; }
        public int Months { get; private set; }
        public int Days { get; private set; }
        public int Hours { get; private set; }
        public int Minutes { get; private set; }
        public int Seconds { get; private set; }
        public int Nanoseconds { get; private set; }
        public bool Negative { get; private set; }

        static bool IsWhite(char c)
        {
            return c == ' ' || c == '\t' || c == '\f' || c == '\r' || c == '\n' || c == '\v';
        }

        static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        enum ParseResult
        {
            Ok,
            Empty,
            Overflow,
            MissingNumber,
            MissingP,
            MissingComponent,
            TrailingText,
            DuplicateTimeSeparator
        };

        static ParseResult ParseNumber(string value, ref int pos, out int? result)
        {
            int startPos = pos;
            result = 0;

            while (pos < value.Length && IsDigit(value[pos]))
            {
                int num = value[pos] - '0';
                if (result > (int.MaxValue - num) / 10)
                {
                    return ParseResult.Overflow;
                }
                result = result * 10 + num;
                ++pos;
            }
            return startPos != pos ? ParseResult.Ok : ParseResult.MissingNumber;
        }

        static ParseResult TryParseInternal(string value, out XsdDuration result)
        {
            result = new XsdDuration();
            if (value == null)
            {
                return ParseResult.Empty;
            }

            // skip leading white space
            int pos = 0;
            for (; pos < value.Length && IsWhite(value[pos]); ++pos)
            {
            }

            // ignore trailing white space
            int length = value.Length;
            for (; length > 0 && IsWhite(value[length - 1]); --length)
            {
            }

            // check for an empty string
            if (pos >= length)
            {
                return ParseResult.Empty;
            }

            // handle negative durations
            if (value[pos] == '-')
            {
                result.Negative = true;
                ++pos;
            }

            // P must be present
            if (pos >= length || value[pos] != 'P')
            {
                return ParseResult.MissingP;
            }
            ++pos;

            // duration might be time only
            bool seenTime = false;
            if (pos < length && value[pos] == 'T')
            {
                seenTime = true;
                ++pos;
            }

            int? num = null;
            Func<ParseResult> getNumber = () => ParseNumber(value, ref pos, out num);

            // get the first component
            ParseResult parseResult = getNumber();
            if (parseResult != ParseResult.Ok)
            {
                return parseResult;
            }

            Func<ParseResult> nextComponent = () =>
            {
                ++pos;
                if (pos < length)
                {
                    if (value[pos] == 'T')
                    {
                        if (seenTime)
                        {
                            return ParseResult.DuplicateTimeSeparator;
                        }
                        seenTime = true;
                        ++pos;
                    }
                    return getNumber();
                }
                else
                {
                    num = null;
                }
                return ParseResult.Ok;
            };

            if (!seenTime)
            {
                if (pos < length && value[pos] == 'Y')
                {
                    result.Years = num.Value;
                    if ((parseResult = nextComponent()) != ParseResult.Ok)
                    {
                        return parseResult;
                    }
                }
                if (!seenTime && (pos < length && value[pos] == 'M'))
                {
                    result.Months = num.Value;
                    if ((parseResult = nextComponent()) != ParseResult.Ok)
                    {
                        return parseResult;
                    }
                }
                if (!seenTime && (pos < length && value[pos] == 'D'))
                {
                    result.Days = num.Value;
                    if ((parseResult = nextComponent()) != ParseResult.Ok)
                    {
                        return parseResult;
                    }
                }
                if (!seenTime && num.HasValue)
                {
                    return ParseResult.MissingComponent;
                }
            }
            if (seenTime)
            {
                if (pos < length && value[pos] == 'H')
                {
                    result.Hours = num.Value;
                    if ((parseResult = nextComponent()) != ParseResult.Ok)
                    {
                        return parseResult;
                    }
                }
                if (pos < length && value[pos] == 'M')
                {
                    result.Minutes = num.Value;
                    if ((parseResult = nextComponent()) != ParseResult.Ok)
                    {
                        return parseResult;
                    }
                }
                if (pos < length && value[pos] == '.')
                {
                    result.Seconds = num.Value;
                    ++pos;

                    int fraction = 0;
                    int numDigits = 0;
                    while (pos < length && IsDigit(value[pos]))
                    {
                        num = value[pos++] - '0';
                        if (fraction <= (int.MaxValue - num.Value) / 10)
                        {
                            fraction = fraction * 10 + num.Value;
                            ++numDigits;
                        }
                    }
                    if (numDigits == 0)
                    {
                        return ParseResult.MissingNumber;
                    }
                    // if we've got more than 9 digits then truncate to nanosecond precision
                    for (; numDigits > 9; --numDigits)
                    {
                        fraction /= 10;
                    }
                    // less than 9 digits means 10^(9-digits) nanoseconds,
                    // .1 is 10^8 ns, .01 is 10^7ns, etc
                    for (; numDigits < 9; ++numDigits)
                    {
                        fraction *= 10;
                    }
                    result.Nanoseconds = fraction;

                    // we have to have 'S' after a decimal number
                    if (pos >= length || value[pos] != 'S')
                    {
                        return ParseResult.MissingComponent;
                    }
                    ++pos;
                    num = null;
                }
                else if (pos < length && value[pos] == 'S')
                {
                    result.Seconds = num.Value;
                    ++pos;
                    num = null;
                }
                if (num.HasValue)
                {
                    return ParseResult.MissingComponent;
                }
            }
            if (pos != length)
            {
                return ParseResult.TrailingText;
            }
            return ParseResult.Ok;
        }

        public static bool TryParse(string value, out XsdDuration result)
        {
            return TryParseInternal(value, out result) == ParseResult.Ok;
        }

        public static XsdDuration Parse(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(value);
            }

            XsdDuration result;
            var parseResult = TryParseInternal(value, out result);
            switch (parseResult)
            {
                case ParseResult.Ok:
                    break;
                case ParseResult.Overflow:
                    throw new OverflowException($"number larger than {int.MaxValue}");
                case ParseResult.MissingComponent:
                    throw new FormatException("expected a duration component specifier");
                case ParseResult.MissingNumber:
                    throw new FormatException("expected a number");
                case ParseResult.MissingP:
                    throw new FormatException("the duration string does not start with 'P'");
                case ParseResult.Empty:
                    throw new FormatException("the duration string is empty");
                case ParseResult.TrailingText:
                    throw new FormatException("trailing text was found after the duration string");
                case ParseResult.DuplicateTimeSeparator:
                    throw new FormatException("duplicate time separator");
                default:
                    throw new InvalidOperationException($"internal error -- unhandled parse result '{parseResult}'");
            }
            return result;
        }

        public static string ToString(XsdDuration value)
        {
            var result = new StringBuilder();
            if (value.Negative)
            {
                result.Append('-');
            }
            result.Append('P');
            int len = result.Length;

            if (value.Years != 0)
            {
                result.Append(value.Years);
                result.Append('Y');
            }
            if (value.Months != 0)
            {
                result.Append(value.Months);
                result.Append('M');
            }
            if (value.Days != 0)
            {
                result.Append(value.Days);
                result.Append('D');
            }
            if (value.Hours != 0 || value.Minutes != 0 ||
                value.Seconds != 0 || value.Nanoseconds != 0)
            {
                result.Append('T');
                if (value.Hours != 0)
                {
                    result.Append(value.Hours);
                    result.Append('H');
                }
                if (value.Minutes != 0)
                {
                    result.Append(value.Minutes);
                    result.Append('M');
                }
                if (value.Nanoseconds != 0)
                {
                    result.Append(value.Seconds);
                    result.Append('.');
                    // nanoseconds are 10^-9s so pad up to 9 digits with leading zeroes
                    // trailing zeroes are superfluous so can be trimmed
                    // 010000000 = .01
                    // 000000010 = .000000010
                    // 100000000 = .1
                    // 120000000 = .12
                    // 123456789 = .123456789
                    string ns = value.Nanoseconds.ToString("000000000").TrimEnd(new char[] { '0' });
                    result.Append(ns);
                    result.Append('S');
                }
                else if (value.Seconds != 0)
                {
                    result.Append(value.Seconds);
                    result.Append('S');
                }
            }
            if (result.Length == len)
            {
                result.Append("T0S");
            }
            return result.ToString();
        }

        public override string ToString()
        {
            return ToString(this);
        }

        public override bool Equals(object obj)
        {
            if (obj is XsdDuration other)
            {
                return Negative == other.Negative &&
                    Years == other.Years && Months == other.Months && Days == other.Days &&
                    Hours == other.Hours && Minutes == other.Minutes && Seconds == other.Seconds &&
                    Nanoseconds == other.Nanoseconds;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int result = 17;
            result = result * 31 + (Negative ? 1 : 0);
            result = result * 31 + Years;
            result = result * 31 + Months;
            result = result * 31 + Days;
            result = result * 31 + Hours;
            result = result * 31 + Minutes;
            result = result * 31 + Seconds;
            result = result * 31 + Nanoseconds;
            return result;
        }

        public TimeSpan ToTimeSpan()
        {
            checked
            {
                long ticks;
                ticks = ((((Years * 365 + Months * 30 + Days) * 24 + Hours)
                    * 60 + Minutes) * 60 + Seconds) * TimeSpan.TicksPerSecond
                    + Nanoseconds / 100;
                return new TimeSpan(Negative ? -ticks : ticks);
            }
        }

        public XsdDuration(bool negative = false, int years = 0, int months = 0, int days = 0,
            int hours = 0, int minutes = 0, int seconds = 0, int nanoseconds = 0)
        {
            Negative = negative;
            Years = years;
            Months = months;
            Days = days;
            Hours = hours;
            Minutes = minutes;
            Seconds = seconds;
            Nanoseconds = nanoseconds;
        }

        public XsdDuration(string value)
        {
            this = Parse(value);
        }

        public XsdDuration(TimeSpan timeSpan)
        {
            Negative = timeSpan.Ticks < 0;
            if (timeSpan.Ticks <= long.MinValue)
            {
                throw new OverflowException($"The timespan is less than or equal to {long.MinValue} ticks");
            }

            long ticks = Math.Abs(timeSpan.Ticks);
            Years = 0;
            Months = 0;
            Days = (int)(ticks / TimeSpan.TicksPerDay);
            Hours = ((int)(ticks / TimeSpan.TicksPerHour)) % 24;
            Minutes = ((int)(ticks / TimeSpan.TicksPerMinute)) % 60;
            Seconds = ((int)(ticks / TimeSpan.TicksPerSecond)) % 60;
            Nanoseconds = (int)((ticks % TimeSpan.TicksPerSecond) * 100);
        }
    }
}
