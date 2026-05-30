using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PackagingInspectionTools.UI
{
    internal static class UiStyles
    {
        public static readonly Color WindowBackColor = Color.FromArgb(244, 247, 250);
        public static readonly Color SurfaceBackColor = Color.White;
        public static readonly Color BorderColor = Color.FromArgb(211, 219, 229);
        public static readonly Color HeaderBackColor = Color.FromArgb(34, 64, 98);
        public static readonly Color HeaderForeColor = Color.White;
        public static readonly Color ButtonBackColor = Color.FromArgb(42, 101, 176);
        public static readonly Color ButtonForeColor = Color.White;
        public static readonly Color SecondaryTextColor = Color.FromArgb(77, 88, 103);

        public static void ApplyPage(Control control)
        {
            control.BackColor = WindowBackColor;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = SurfaceBackColor;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = BorderColor;
            grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderBackColor;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = HeaderForeColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderBackColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = HeaderForeColor;
            grid.ColumnHeadersHeight = 32;
            grid.DefaultCellStyle.BackColor = SurfaceBackColor;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 235, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 30, 42);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        }

        public static void StyleButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = ButtonBackColor;
            button.ForeColor = ButtonForeColor;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.AutoEllipsis = true;
            button.MinimumSize = new Size(96, 32);
        }

        public static int GetButtonWidth(string text, Font font, int minimumWidth)
        {
            var measured = TextRenderer.MeasureText(text, font).Width + 30;
            return measured > minimumWidth ? measured : minimumWidth;
        }

        public static void StyleInput(Control control)
        {
            control.BackColor = SurfaceBackColor;
            control.ForeColor = Color.FromArgb(26, 34, 44);
            control.Margin = new Padding(0, 2, 2, 4);
        }

        public static Label FieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = SecondaryTextColor,
                Margin = new Padding(0, 0, 8, 0)
            };
        }

        public static TableLayoutPanel CreateLabeledField(string labelText, Control editor)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Margin = new Padding(0, 0, 6, 0)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(FieldLabel(labelText), 0, 0);
            editor.Dock = DockStyle.Fill;
            StyleInput(editor);
            panel.Controls.Add(editor, 0, 1);
            return panel;
        }

        public static Icon CreateApplicationIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var background = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), Color.FromArgb(24, 93, 160), Color.FromArgb(31, 136, 99), 45F))
            using (var pen = new Pen(Color.White, 3F))
            using (var smallPen = new Pen(Color.FromArgb(225, 255, 255, 255), 2F))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillRoundedRectangle(background, new Rectangle(1, 1, 30, 30), 7);
                graphics.DrawLine(pen, 9, 22, 15, 11);
                graphics.DrawLine(pen, 15, 11, 21, 22);
                graphics.DrawLine(smallPen, 8, 23, 24, 23);
                return Icon.FromHandle(bitmap.GetHicon());
            }
        }

        private static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
        {
            using (var path = new GraphicsPath())
            {
                var diameter = radius * 2;
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                graphics.FillPath(brush, path);
            }
        }
    }
}
