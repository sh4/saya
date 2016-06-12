using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace saya.core
{
    /// <summary>
    /// Alcore Abbreviation Scoring Algorithm
    /// https://github.com/quicksilver/Quicksilver/blob/master/Quicksilver/Code-QuickStepFoundation/NSString_BLTRExtensions.h
    /// http://steps.dodgson.org/bn/2009/09/12/
    /// </summary>
    public class AlcorAbbreviationScorer
    {
        [System.Diagnostics.DebuggerDisplay("{ToString()}")]
        class StringRange
        {
            public string Text { get; private set; }
            public int Location { get; set; }
            public int Length { get; set; }

            public int Max => Location + Length;

            public char this[int i] => Text[i];

            public StringRange(string value)
            {
                Text = value;
            }

            public string Substring(int count)
            {
                return Text.Substring(Location, count);
            }

            public int IndexOf(string value)
            {
                return Text.IndexOf(value, Location, Length, StringComparison.InvariantCultureIgnoreCase);
            }

            public StringRange IndexOfRange(string value)
            {
                var matchLocation = IndexOf(value);
                if (matchLocation == -1)
                {
                    return null;
                }
                return new StringRange(Text)
                {
                    Location = matchLocation,
                    Length = value.Length,
                };
            }

            public override string ToString()
            {
                return Text.Substring(Location, Length);
            }
        }

        public static float Compute(string text, string abbreviation)
        {
            return Compute(
                new StringRange(text) { Length = text.Length },
                new StringRange(abbreviation) { Length = abbreviation.Length });
        }

        private static float Compute(StringRange searchRange, StringRange abbreviationRange)
        {
            if (abbreviationRange.Length == 0)
            {
                return 0.9f;
            }

            if (searchRange.Location + abbreviationRange.Length > searchRange.Text.Length)
            {
                return 0.0f;
            }

            for (var i = abbreviationRange.Length; i > 0; i--)
            {
                var matchedRange = searchRange.IndexOfRange(abbreviationRange.Substring(i));
                if (matchedRange == null ||
                    matchedRange.Location + abbreviationRange.Length > searchRange.Text.Length)
                {
                    continue;
                }

                var remainingSearchRange = new StringRange(searchRange.Text)
                {
                    Location = matchedRange.Max,
                };
                remainingSearchRange.Length = searchRange.Max - remainingSearchRange.Location;

                var remainingScore = Compute(
                    remainingSearchRange,
                    new StringRange(abbreviationRange.Text)
                    {
                        Location = abbreviationRange.Location + i,
                        Length = abbreviationRange.Length - i,
                    });

                if (remainingScore > 0.0f)
                {
                    float score = remainingSearchRange.Location - searchRange.Location;
                    if (matchedRange.Location > searchRange.Location)
                    {
                        if (char.IsWhiteSpace(matchedRange[matchedRange.Location - 1])) // マッチ文字列の 1 つ前が空白文字
                        {
                            // 検索開始オフセットからマッチ文字列の 2 つ前までの文字のスコアを計算
                            // * 空白文字 -> 0点
                            // * 空白以外の文字 -> 0.85 点
                            for (var j = matchedRange.Location - 2; j >= searchRange.Location; --j)
                            {
                                score -= char.IsWhiteSpace(matchedRange[j]) ? 1.0f : 0.15f;
                            }
                        }
                        else if (char.IsUpper(matchedRange[matchedRange.Location])) // マッチ文字列の先頭が大文字
                        {
                            // 検索開始オフセットからマッチ文字列の 1 つ前までの文字のスコアを調整
                            // * 大文字 -> 0点
                            // * 大文字以外の文字 -> 0.85 点
                            for (var j = matchedRange.Location - 1; j >= searchRange.Location; --j)
                            {
                                score -= char.IsUpper(matchedRange[j]) ? 1.0f : 0.15f;
                            }
                        }
                        else
                        {
                            // 検索開始位置からマッチ文字列の位置までのスキップした文字はすべて 0 点
                            score -= matchedRange.Location - searchRange.Location;
                        }
                    }
                    score += remainingScore * remainingSearchRange.Length;
                    score /= searchRange.Length;
                    return score;
                }
            }

            return 0.0f;
        }
    }
}
