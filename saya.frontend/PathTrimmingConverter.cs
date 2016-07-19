using System;
using System.IO;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace saya.frontend
{
    class PathTrimmingConverter : IMultiValueConverter
    {
        private class MeasureContext
        {
            public StringBuilder Builder { get; } = new StringBuilder();
            public double RemainWidth { get; set; }
            public GlyphTypeface Typeface { get; set; }
            public double FontSize { get; set; }
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var textBlock = (TextBlock)values[0];
            var text = (string)values[1];
            var remainWidth = (double)values[2];

            var typeface = new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch);

            GlyphTypeface t;
            if (!typeface.TryGetGlyphTypeface(out t))
            {
                throw new InvalidOperationException();
            }

            var context = new MeasureContext
            {
                FontSize = textBlock.FontSize,
                RemainWidth = textBlock.ActualWidth,
                Typeface = t,
            };

            if (!MeasureGlyphsWidth("...", context))
            {
                // 省略記号（...）も全部入らなかった
                return context.Builder.ToString();
            }

            var fileName = Path.GetFileName(text);
            var trimmedText = context.Builder.ToString();

            // 文字列（ファイル名）の末尾から文字列長を計算
            if (!MeasureGlyphsWidth(fileName.Reverse(), context))
            {
                // ファイル名を最後まで入れられなかったので、バッファ内にある逆順のファイル名（一部）を元に戻して返す
                var partialFileName = new string(context.Builder.ToString().Reverse().ToArray());
                return trimmedText + partialFileName;
            }

            // ファイル名は全部入る
            trimmedText += fileName;

            var directoryPath = Path.GetDirectoryName(text);

            if (!string.IsNullOrWhiteSpace(directoryPath)&& !MeasureGlyphsWidth(directoryPath, context))
            {
                // パスの途中までしか入らなかった
                return context.Builder.ToString() + trimmedText;
            }

            // すべての文字列が入ったので、それを返す

            // ディレクトリとパスの間にある区切り文字の幅を計算していないが、
            // 最初に "..." の幅を加算しているので問題にはならないはず

            // ディレクトリがルートの場合(C:\)も加味していないが、同様の理由で問題にはならないはず...
            return text;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private bool MeasureGlyphsWidth(IEnumerable<char> glyphs, MeasureContext context)
        {
            context.Builder.Clear();

            var t = context.Typeface;
            var ok = true;
            foreach (var c in glyphs)
            {
                var key = t.CharacterToGlyphMap[c];
                context.RemainWidth -= t.AdvanceWidths[key] * context.FontSize;
                if (context.RemainWidth < 0)
                {
                    ok = false;
                    break;
                }
                context.Builder.Append(c);
            }
            return ok;
        }
    }
}
