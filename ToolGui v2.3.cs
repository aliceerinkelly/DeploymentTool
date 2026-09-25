// ============================================================================
// ToolGui.cs - Unified Windows Deployment, AppX & AGC Solid Suite v2.3
// Copyright (c) 2026 Alice Kelly. All rights reserved.
// ============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: SupportedOSPlatform("windows7.0")]

// Assembly metadata
[assembly: AssemblyTitle("Alice Kelly - Windows Deployment Suite v2.3")]
[assembly: AssemblyDescription("Unified Windows Deployment, AppX Servicing & AGC Solid Compression Suite")]
[assembly: AssemblyCompany("Alice Kelly")]
[assembly: AssemblyProduct("DeploymentUtility")]
[assembly: AssemblyCopyright("Copyright © 2026 Alice Kelly. All rights reserved.")]
[assembly: AssemblyVersion("2.3.0.0")]
[assembly: AssemblyFileVersion("2.3.0.0")]

namespace DeploymentUtility
{
    // ========================================================================
    // 3D GLASS GLOW BUTTON (NO EDGE CLIPPING / NO SPIKES)
    // ========================================================================
    public class ModernGlowButton : Button
    {
        private Color _accentColor = Color.FromArgb(0, 255, 136);
        private int _cornerRadius = 11;
        private bool _isHovered = false;
        private bool _isPressed = false;
        public bool IsDarkMode { get; set; } = true;

        public Color AccentColor
        {
            get => _accentColor;
            set { _accentColor = value; Invalidate(); }
        }

        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; Invalidate(); }
        }

        public ModernGlowButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Color.FromArgb(240, 245, 245);
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; _isPressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _isPressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _isPressed = false; Invalidate(); }

        public static GraphicsPath GetPillPath(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color themeBg = IsDarkMode ? Color.FromArgb(14, 16, 16) : Color.FromArgb(242, 244, 246);
            g.Clear(themeBg);

            float yOffset = _isPressed ? 1.5f : 0f;
            RectangleF bodyRect = new RectangleF(4f, 4f + yOffset, Width - 8f, Height - 8f);

            // 1. Soft Outer Neon Bloom on Hover
            if (_isHovered)
            {
                RectangleF glowRect = new RectangleF(bodyRect.X - 1.5f, bodyRect.Y - 1.5f, bodyRect.Width + 3f, bodyRect.Height + 3f);
                using (GraphicsPath glowPath = GetPillPath(glowRect, _cornerRadius + 1.5f))
                using (Pen glowPen = new Pen(Color.FromArgb(IsDarkMode ? 50 : 35, _accentColor), 2f))
                {
                    glowPen.LineJoin = LineJoin.Round;
                    g.DrawPath(glowPen, glowPath);
                }
            }

            // 2. 3D Body Surface Plate
            using (GraphicsPath bodyPath = GetPillPath(bodyRect, _cornerRadius))
            {
                Color topShade, bottomShade;
                if (IsDarkMode)
                {
                    topShade = _isPressed ? Color.FromArgb(16, 20, 22) : (_isHovered ? Color.FromArgb(42, 50, 54) : Color.FromArgb(28, 34, 38));
                    bottomShade = _isPressed ? Color.FromArgb(28, 34, 38) : (_isHovered ? Color.FromArgb(20, 24, 26) : Color.FromArgb(15, 18, 20));
                }
                else
                {
                    topShade = _isPressed ? Color.FromArgb(220, 224, 228) : (_isHovered ? Color.FromArgb(255, 255, 255) : Color.FromArgb(246, 248, 250));
                    bottomShade = _isPressed ? Color.FromArgb(236, 240, 244) : (_isHovered ? Color.FromArgb(230, 235, 240) : Color.FromArgb(222, 228, 234));
                }

                using (LinearGradientBrush fillBrush = new LinearGradientBrush(bodyRect, topShade, bottomShade, 90f))
                {
                    g.FillPath(fillBrush, bodyPath);
                }

                // 3. Clean Neon Rim
                using (Pen rimPen = new Pen(_accentColor, _isHovered ? 1.6f : 1.2f))
                {
                    rimPen.LineJoin = LineJoin.Round;
                    g.DrawPath(rimPen, bodyPath);
                }

                // 4. Glass Specular Highlight (Acrylic 3D Sheen)
                if (!_isPressed)
                {
                    RectangleF highlightRect = new RectangleF(bodyRect.X + 2f, bodyRect.Y + 1f, bodyRect.Width - 4f, (bodyRect.Height / 2f) - 1f);
                    using (GraphicsPath hlPath = GetPillPath(highlightRect, _cornerRadius - 2))
                    using (LinearGradientBrush hlBrush = new LinearGradientBrush(
                        highlightRect,
                        Color.FromArgb(_isHovered ? 70 : 40, Color.White),
                        Color.FromArgb(0, Color.White),
                        90f))
                    {
                        g.FillPath(hlBrush, hlPath);
                    }
                }
            }

            Rectangle textRect = new Rectangle(0, (int)yOffset, Width, Height);
            Color textCol = IsDarkMode ? ForeColor : Color.FromArgb(25, 32, 36);
            TextRenderer.DrawText(g, Text, Font, textRect, textCol,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | 
                TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
        }
    }

    // ========================================================================
    // 3D BROWSER TAB STRIP
    // ========================================================================
    public class ModernGlowTabStrip : Control
    {
        public string[] Tabs { get; set; } = new string[] { "Windows Deployment Hub", "AppXPulse", "AGC Solid Engine" };
        public int SelectedIndex { get; set; } = 0;
        public event Action<int>? TabChanged;
        public Color AccentColor { get; set; } = Color.FromArgb(0, 255, 136);
        public bool IsDarkMode { get; set; } = true;

        private int _hoverIndex = -1;

        public ModernGlowTabStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 44;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        private RectangleF GetTabRect(int index)
        {
            float tabWidth = (Width - 36f) / Tabs.Length;
            return new RectangleF(18f + (index * tabWidth), 4f, tabWidth - 10f, Height - 8f);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int prev = _hoverIndex;
            _hoverIndex = -1;
            for (int i = 0; i < Tabs.Length; i++)
            {
                if (GetTabRect(i).Contains(e.Location)) { _hoverIndex = i; break; }
            }
            if (prev != _hoverIndex) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hoverIndex = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            for (int i = 0; i < Tabs.Length; i++)
            {
                if (GetTabRect(i).Contains(e.Location))
                {
                    if (SelectedIndex != i)
                    {
                        SelectedIndex = i;
                        Invalidate();
                        TabChanged?.Invoke(i);
                    }
                    break;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color themeBg = IsDarkMode ? Color.FromArgb(14, 16, 16) : Color.FromArgb(242, 244, 246);
            g.Clear(themeBg);

            for (int i = 0; i < Tabs.Length; i++)
            {
                RectangleF r = GetTabRect(i);
                bool isSelected = (i == SelectedIndex);
                bool isHovered = (i == _hoverIndex);

                if (isSelected)
                {
                    Color topShade = IsDarkMode ? Color.FromArgb(32, 38, 42) : Color.FromArgb(255, 255, 255);
                    Color btmShade = IsDarkMode ? Color.FromArgb(16, 20, 22) : Color.FromArgb(232, 236, 240);

                    using (GraphicsPath path = ModernGlowButton.GetPillPath(r, 8f))
                    {
                        using (LinearGradientBrush brush = new LinearGradientBrush(r, topShade, btmShade, 90f))
                        {
                            g.FillPath(brush, path);
                        }
                        using (Pen p = new Pen(AccentColor, 1.4f))
                        {
                            p.LineJoin = LineJoin.Round;
                            g.DrawPath(p, path);
                        }
                    }

                    RectangleF barRect = new RectangleF(r.X + 16f, r.Bottom - 3.5f, r.Width - 32f, 2.8f);
                    using (GraphicsPath barPath = ModernGlowButton.GetPillPath(barRect, 1.4f))
                    using (SolidBrush barBrush = new SolidBrush(AccentColor))
                    {
                        g.FillPath(barBrush, barPath);
                    }
                }
                else if (isHovered)
                {
                    using (GraphicsPath path = ModernGlowButton.GetPillPath(r, 8f))
                    {
                        using (SolidBrush hBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(26, 32, 34) : Color.FromArgb(235, 238, 242)))
                        {
                            g.FillPath(hBrush, path);
                        }
                        using (Pen hPen = new Pen(Color.FromArgb(60, AccentColor), 1f))
                        {
                            g.DrawPath(hPen, path);
                        }
                    }
                }

                Color textCol;
                if (isSelected) textCol = IsDarkMode ? Color.White : Color.FromArgb(18, 22, 26);
                else if (isHovered) textCol = IsDarkMode ? Color.FromArgb(230, 235, 240) : Color.FromArgb(50, 60, 70);
                else textCol = IsDarkMode ? Color.FromArgb(140, 150, 155) : Color.FromArgb(120, 130, 135);

                Rectangle textRect = new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height - (isSelected ? 2 : 0));
                TextRenderer.DrawText(g, Tabs[i], Font, textRect, textCol,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    // ========================================================================
    // 3D GLOWING CONSOLE PANEL
    // ========================================================================
    public class ModernGlowConsolePanel : Panel
    {
        private Color _accentColor = Color.FromArgb(0, 255, 136);
        private int _cornerRadius = 12;
        private bool _isFocused = false;
        public bool IsDarkMode { get; set; } = true;

        public bool IsConsoleFocused
        {
            get => _isFocused;
            set { _isFocused = value; Invalidate(); }
        }

        public Color AccentColor
        {
            get => _accentColor;
            set { _accentColor = value; Invalidate(); }
        }

        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; Invalidate(); }
        }

        public ModernGlowConsolePanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Padding = new Padding(12, 10, 12, 10);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color themeBg = IsDarkMode ? Color.FromArgb(14, 16, 16) : Color.FromArgb(242, 244, 246);
            g.Clear(themeBg);

            RectangleF bodyRect = new RectangleF(3f, 3f, Width - 6f, Height - 6f);

            if (_isFocused)
            {
                RectangleF glowRect = new RectangleF(bodyRect.X - 1.5f, bodyRect.Y - 1.5f, bodyRect.Width + 3f, bodyRect.Height + 3f);
                using (GraphicsPath glowPath = ModernGlowButton.GetPillPath(glowRect, _cornerRadius + 1.5f))
                using (Pen glowPen = new Pen(Color.FromArgb(IsDarkMode ? 45 : 30, _accentColor), 2f))
                {
                    glowPen.LineJoin = LineJoin.Round;
                    g.DrawPath(glowPen, glowPath);
                }
            }

            using (GraphicsPath bodyPath = ModernGlowButton.GetPillPath(bodyRect, _cornerRadius))
            {
                Color topShade = IsDarkMode ? Color.FromArgb(8, 10, 12) : Color.FromArgb(254, 255, 255);
                Color bottomShade = IsDarkMode ? Color.FromArgb(14, 18, 20) : Color.FromArgb(246, 248, 250);

                using (LinearGradientBrush fillBrush = new LinearGradientBrush(bodyRect, topShade, bottomShade, 90f))
                {
                    g.FillPath(fillBrush, bodyPath);
                }

                using (Pen rimPen = new Pen(_accentColor, _isFocused ? 1.6f : 1.2f))
                {
                    rimPen.LineJoin = LineJoin.Round;
                    g.DrawPath(rimPen, bodyPath);
                }
            }
        }
    }

    // ========================================================================
    // 3D NEON TOGGLE SWITCH
    // ========================================================================
    public class ModernGlowToggleSwitch : Control
    {
        private bool _isToggled = true;
        private Color _accentColor = Color.FromArgb(0, 255, 136);
        private bool _isHovered = false;

        public event Action<bool>? Toggled;

        public bool IsDarkMode
        {
            get => _isToggled;
            set { _isToggled = value; Invalidate(); }
        }

        public Color AccentColor
        {
            get => _accentColor;
            set { _accentColor = value; Invalidate(); }
        }

        public ModernGlowToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Size = new Size(54, 46);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            _isToggled = !_isToggled;
            Invalidate();
            Toggled?.Invoke(_isToggled);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color themeBg = _isToggled ? Color.FromArgb(14, 16, 16) : Color.FromArgb(242, 244, 246);
            g.Clear(themeBg);

            using (Font f = new Font("Segoe UI", 7f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(_isToggled ? Color.FromArgb(140, 150, 150) : Color.FromArgb(90, 100, 105)))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(_isToggled ? "DARK" : "LIGHT", f, textBrush, new RectangleF(0, 0, Width, 13), sf);
            }

            RectangleF trackRect = new RectangleF(6f, 16f, 42f, 22f);
            using (GraphicsPath trackPath = ModernGlowButton.GetPillPath(trackRect, 11f))
            {
                Color topTrack = _isToggled ? Color.FromArgb(20, 24, 26) : Color.FromArgb(215, 220, 225);
                Color btmTrack = _isToggled ? Color.FromArgb(32, 38, 42) : Color.FromArgb(240, 244, 248);

                using (LinearGradientBrush trackBrush = new LinearGradientBrush(trackRect, topTrack, btmTrack, 90f))
                {
                    g.FillPath(trackBrush, trackPath);
                }

                using (Pen trackPen = new Pen(_isHovered ? _accentColor : (_isToggled ? Color.FromArgb(50, 60, 65) : Color.FromArgb(180, 190, 195)), 1.2f))
                {
                    trackPen.LineJoin = LineJoin.Round;
                    g.DrawPath(trackPen, trackPath);
                }

                float knobD = 16f;
                float knobX = _isToggled ? (trackRect.Right - knobD - 3f) : (trackRect.X + 3f);
                float knobY = trackRect.Y + 3f;
                RectangleF knobRect = new RectangleF(knobX, knobY, knobD, knobD);

                using (LinearGradientBrush knobBrush = new LinearGradientBrush(knobRect,
                    _isToggled ? _accentColor : Color.White,
                    _isToggled ? ControlPaint.Dark(_accentColor, 0.2f) : Color.FromArgb(210, 215, 220), 90f))
                {
                    g.FillEllipse(knobBrush, knobRect);
                }

                using (Pen knobRim = new Pen(Color.White, 0.8f))
                {
                    g.DrawEllipse(knobRim, knobRect);
                }
            }
        }
    }

    // ========================================================================
    // STANDALONE THEME COLOR PICKER
    // ========================================================================
    public class ThemePaletteBar : Control
    {
        public Color[] Colors { get; set; }
        public int SelectedIndex { get; set; } = 0;
        public event Action<Color>? ColorSelected;
        public bool IsDarkMode { get; set; } = true;

        private int _hoverIndex = -1;
        private int _pressedIndex = -1;
        private const int SwatchWidth = 22;
        private const int SwatchHeight = 18;
        private const int Spacing = 6;
        private const int TopOffset = 18;

        public ThemePaletteBar(Color[] palettes, Color initialAccent)
        {
            Colors = palettes;
            for (int i = 0; i < Colors.Length; i++)
            {
                if (Colors[i].ToArgb() == initialAccent.ToArgb())
                {
                    SelectedIndex = i;
                    break;
                }
            }

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | 
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Width = 46;
            Height = TopOffset + (palettes.Length * (SwatchHeight + Spacing)) + 4;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        private RectangleF GetSwatchRect(int index)
        {
            float y = TopOffset + (index * (SwatchHeight + Spacing));
            float x = (Width - SwatchWidth) / 2f;
            return new RectangleF(x, y, SwatchWidth, SwatchHeight);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int prev = _hoverIndex;
            _hoverIndex = -1;
            for (int i = 0; i < Colors.Length; i++)
            {
                if (GetSwatchRect(i).Contains(e.Location)) { _hoverIndex = i; break; }
            }
            if (prev != _hoverIndex) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hoverIndex = -1; _pressedIndex = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < Colors.Length; i++)
                {
                    if (GetSwatchRect(i).Contains(e.Location)) { _pressedIndex = i; Invalidate(); break; }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressedIndex >= 0)
            {
                if (GetSwatchRect(_pressedIndex).Contains(e.Location))
                {
                    if (SelectedIndex != _pressedIndex)
                    {
                        SelectedIndex = _pressedIndex;
                        ColorSelected?.Invoke(Colors[_pressedIndex]);
                    }
                }
                _pressedIndex = -1;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color themeBg = IsDarkMode ? Color.FromArgb(14, 16, 16) : Color.FromArgb(242, 244, 246);
            g.Clear(themeBg);

            using (Font f = new Font("Segoe UI Semibold", 7f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(140, 150, 150) : Color.FromArgb(100, 110, 115)))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("COLOR", f, textBrush, new RectangleF(0, 0, Width, 15), sf);
            }

            for (int i = 0; i < Colors.Length; i++)
            {
                RectangleF baseRect = GetSwatchRect(i);
                Color c = Colors[i];
                bool isSelected = (i == SelectedIndex);
                bool isHovered = (i == _hoverIndex);
                bool isPressed = (i == _pressedIndex);

                float yOffset = isPressed ? 1.0f : 0f;
                RectangleF tileRect = new RectangleF(baseRect.X, baseRect.Y + yOffset, baseRect.Width, baseRect.Height);

                Color topShade = isHovered ? ControlPaint.Light(c, 0.35f) : ControlPaint.Light(c, 0.15f);
                Color btmShade = ControlPaint.Dark(c, 0.25f);

                using (GraphicsPath tilePath = ModernGlowButton.GetPillPath(tileRect, 5f))
                {
                    using (LinearGradientBrush fillBrush = new LinearGradientBrush(tileRect, topShade, btmShade, 90f))
                    {
                        g.FillPath(fillBrush, tilePath);
                    }

                    Color rimColor = isSelected ? Color.White : (isHovered ? ControlPaint.Light(c, 0.5f) : c);
                    float rimWidth = isSelected ? 1.8f : 1.2f;
                    using (Pen rimPen = new Pen(rimColor, rimWidth))
                    {
                        g.DrawPath(rimPen, tilePath);
                    }

                    if (!isPressed)
                    {
                        RectangleF hlRect = new RectangleF(tileRect.X + 1.2f, tileRect.Y + 0.8f, tileRect.Width - 2.4f, (tileRect.Height / 2f) - 0.8f);
                        using (GraphicsPath hlPath = ModernGlowButton.GetPillPath(hlRect, 3.5f))
                        using (LinearGradientBrush hlBrush = new LinearGradientBrush(
                            hlRect,
                            Color.FromArgb(isHovered ? 130 : 85, Color.White),
                            Color.FromArgb(0, Color.White),
                            90f))
                        {
                            g.FillPath(hlBrush, hlPath);
                        }
                    }
                }
            }
        }
    }

    // ========================================================================
    // AGC SOLID ENGINE TYPES
    // ========================================================================
    public class ArchiveItem
    {
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; } = false;
        public override string ToString() => IsDirectory ? $"{RelativePath}\\ [DIR]" : RelativePath;
    }

    // ========================================================================
    // MAIN MULTI-TAB WINDOW
    // ========================================================================
    public class MainForm : Form
    {
        private Color currentAccent;
        private bool isDarkMode = true;
        private List<ModernGlowButton> registeredButtons = new List<ModernGlowButton>();

        // Navigation
        private ModernGlowTabStrip tabStrip;
        private ThemePaletteBar paletteBar;
        private ModernGlowToggleSwitch toggleMode;
        private Label lblCopyright;

        // Tabs
        private Panel pnlTabDeployment = null!;
        private Panel pnlTabAppXPulse = null!;
        private Panel pnlTabAgc = null!;

        // Tab 1: Deployment Hub
        private TextBox txtDeploymentLog = null!;
        private ModernGlowConsolePanel pnlDeploymentLogBorder = null!;
        private string workDir;
        private string driverBackupDir;
        private string mountDir;
        private string tempWim;
        private string outDir;

        // Tab 2: AppXPulse
        private ListView lvAppX = null!;
        private TextBox txtAppXLog = null!;
        private ModernGlowConsolePanel pnlAppXLogBorder = null!;

        // Tab 3: AGC Solid Engine
        private const uint COMPRESS_ALGORITHM_MSZIP = 2;
        private const uint COMPRESS_ALGORITHM_LZMS = 5;

        [DllImport("cabinet.dll", SetLastError = true)]
        private static extern bool CreateCompressor(uint Algorithm, IntPtr AllocationRoutines, out IntPtr CompressorHandle);
        [DllImport("cabinet.dll", SetLastError = true)]
        private static extern bool Compress(IntPtr CompressorHandle, byte[] UncompressedData, UIntPtr UncompressedDataSize, byte[]? CompressedBuffer, UIntPtr CompressedBufferSize, out UIntPtr CompressedDataSize);
        [DllImport("cabinet.dll", SetLastError = true)]
        private static extern bool CloseCompressor(IntPtr CompressorHandle);
        [DllImport("cabinet.dll", SetLastError = true)]
        private static extern bool CreateDecompressor(uint Algorithm, IntPtr AllocationRoutines, out IntPtr DecompressorHandle);
        [DllImport("cabinet.dll", SetLastError = true)]
        private static extern bool Decompress(IntPtr DecompressorHandle, byte[] CompressedData, UIntPtr CompressedDataSize, byte[]? DecompressedBuffer, UIntPtr DecompressedBufferSize, out UIntPtr DecompressedSize);
        [DllImport("cabinet.dll", SetLastError = true)]
        private static extern bool CloseDecompressor(IntPtr DecompressorHandle);

        private ListBox lstAgcFiles = null!;
        private Label lblAgcOutputDir = null!;
        private ComboBox cmbAgcMethod = null!;
        private CheckBox chkAgcSolid = null!;
        private CheckBox chkAgcTestArchive = null!;
        private CheckBox chkAgcDeleteAfter = null!;
        private Label lblAgcCurrentFile = null!;
        private ProgressBar prgAgcCurrent = null!;
        private Label lblAgcTotal = null!;
        private ProgressBar prgAgcTotal = null!;
        private ModernGlowButton btnAgcBackground = null!;
        private TextBox txtAgcLog = null!;
        private ModernGlowConsolePanel pnlAgcLogBorder = null!;
        private Thread? activeWorkerThread = null;
        private bool isAgcBackgroundMode = false;
        private string selectedOutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public static readonly Color[] Palettes = new Color[]
        {
            Color.FromArgb(0, 255, 136),   // Neon Green (Default)
            Color.FromArgb(0, 229, 255),   // Cyber Cyan
            Color.FromArgb(244, 143, 177),  // Soft Pink
            Color.FromArgb(255, 64, 129),   // Electric Pink / Magenta
            Color.FromArgb(255, 179, 0),    // Amber Glow
            Color.FromArgb(0, 153, 255),    // Deep Azure
            Color.FromArgb(255, 112, 67),   // Sunset Orange
            Color.FromArgb(179, 136, 255)   // Lavender Violet
        };

        public MainForm()
        {
            outDir = @"C:\drivers_backup";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (baseDir.Contains(@"bin\Debug") || baseDir.Contains(@"bin\Release"))
            {
                DirectoryInfo? dirInfo = new DirectoryInfo(baseDir);
                while (dirInfo != null && (dirInfo.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                           dirInfo.Name.Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
                                           dirInfo.Name.Equals("Release", StringComparison.OrdinalIgnoreCase) ||
                                           dirInfo.Name.StartsWith("net", StringComparison.OrdinalIgnoreCase)))
                {
                    dirInfo = dirInfo.Parent;
                }
                workDir = dirInfo != null ? dirInfo.FullName : AppDomain.CurrentDomain.BaseDirectory;
            }
            else
            {
                workDir = AppDomain.CurrentDomain.BaseDirectory;
            }

            driverBackupDir = Path.Combine(workDir, "ExportedDrivers");
            mountDir = Path.Combine(workDir, "WimMount");
            tempWim = Path.Combine(workDir, "temp_processing.wim");

            currentAccent = LoadSavedAccent();
            isDarkMode = LoadSavedThemeMode();

            Rectangle screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
            int formWidth = (int)(screen.Width * 0.74);
            int formHeight = (int)(screen.Height * 0.88);

            this.Text = "Alice Kelly - Windows Deployment Suite v2.3";
            this.Size = new Size(formWidth, formHeight);
            this.MinimumSize = new Size(920, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Tabs Header Strip
            tabStrip = new ModernGlowTabStrip();
            tabStrip.Location = new Point(14, 8);
            tabStrip.Size = new Size(this.ClientSize.Width - 360, 44);
            tabStrip.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tabStrip.AccentColor = currentAccent;
            tabStrip.IsDarkMode = isDarkMode;
            tabStrip.TabChanged += SwitchTab;
            this.Controls.Add(tabStrip);

            lblCopyright = new Label();
            lblCopyright.Text = "© 2026 Alice Kelly • v2.3";
            lblCopyright.Font = new Font("Segoe UI", 9, FontStyle.Italic);
            lblCopyright.Location = new Point(this.ClientSize.Width - 230, 18);
            lblCopyright.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblCopyright.AutoSize = true;
            this.Controls.Add(lblCopyright);

            // Palette Switcher
            paletteBar = new ThemePaletteBar(Palettes, currentAccent);
            paletteBar.Location = new Point(this.ClientSize.Width - 52, 64);
            paletteBar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            paletteBar.ColorSelected += (newColor) =>
            {
                ApplyAccentTheme(newColor);
                SaveAccentPreference(newColor);
            };
            this.Controls.Add(paletteBar);

            // Dark / Light Switch
            toggleMode = new ModernGlowToggleSwitch();
            toggleMode.Location = new Point(this.ClientSize.Width - 56, 260);
            toggleMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            toggleMode.IsDarkMode = isDarkMode;
            toggleMode.AccentColor = currentAccent;
            toggleMode.Toggled += (isDark) =>
            {
                SetDarkMode(isDark);
                SaveThemeModePreference(isDark);
            };
            this.Controls.Add(toggleMode);

            // Build Containers
            InitializeTabDeployment();
            InitializeTabAppXPulse();
            InitializeTabAgc();

            SetDarkMode(isDarkMode);
            ApplyAccentTheme(currentAccent);
            SwitchTab(0);
        }

        private void SwitchTab(int index)
        {
            pnlTabDeployment.Visible = (index == 0);
            pnlTabAppXPulse.Visible = (index == 1);
            pnlTabAgc.Visible = (index == 2);
        }

        private ModernGlowButton CreateRegisterButton(string text, int x, int y, int width, int height, EventHandler handler)
        {
            ModernGlowButton btn = new ModernGlowButton();
            btn.Text = text;
            btn.Location = new Point(x, y);
            btn.Size = new Size(width, height);
            btn.AccentColor = currentAccent;
            btn.IsDarkMode = isDarkMode;
            btn.Click += handler;
            registeredButtons.Add(btn);
            return btn;
        }

        // ====================================================================
        // TAB 1: DEPLOYMENT HUB
        // ====================================================================
        private void InitializeTabDeployment()
        {
            Color themeBg = isDarkMode ? Color.FromArgb(14, 16, 16) : Color.FromArgb(242, 244, 246);
            pnlTabDeployment = new Panel
            {
                Location = new Point(14, 56),
                Size = new Size(this.ClientSize.Width - 78, this.ClientSize.Height - 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = themeBg
            };

            int contentWidth = pnlTabDeployment.Width - 16;
            int col3Width = (contentWidth - 24) / 3;
            int col2Width = (contentWidth - 12) / 2;

            // Row 1: Drivers (3 Columns)
            ModernGlowButton btnBackupDrivers = CreateRegisterButton("Backup Drivers", 6, 4, col3Width, 38, OnBackupDrivers);
            ModernGlowButton btnRestoreDefault = CreateRegisterButton("Restore (Default)", 6 + col3Width + 12, 4, col3Width, 38, OnRestoreDriversDefault);
            ModernGlowButton btnRestoreBrowse = CreateRegisterButton("Restore (Browse...)", 6 + (col3Width * 2) + 24, 4, contentWidth - ((col3Width * 2) + 24), 38, OnRestoreDriversBrowse);

            // Row 2: Images (2 Columns)
            ModernGlowButton btnScanImages = CreateRegisterButton("Scan & Slipstream Image", 6, 48, col2Width, 38, OnScanAndSlipstream);
            ModernGlowButton btnBrowseImage = CreateRegisterButton("Browse Image & Slipstream", 6 + col2Width + 12, 48, contentWidth - (col2Width + 12), 38, OnBrowseAndSlipstream);

            // Row 3: WIM Inspection (2 Columns)
            ModernGlowButton btnListWim = CreateRegisterButton("List WIM Editions / Indexes", 6, 92, col2Width, 38, OnListWimInfo);
            ModernGlowButton btnDeleteIndex = CreateRegisterButton("Delete Unwanted WIM Index", 6 + col2Width + 12, 92, contentWidth - (col2Width + 12), 38, OnDeleteWimIndex);

            // Row 4: Compression (2 Columns)
            ModernGlowButton btnCompressFolder = CreateRegisterButton("Compress Folder (Zip/WinRAR/WinZip)", 6, 136, col2Width, 38, OnCompressFolderPrompt);
            ModernGlowButton btnWimToEsd = CreateRegisterButton("Convert WIM to Solid ESD", 6 + col2Width + 12, 136, contentWidth - (col2Width + 12), 38, OnConvertWimToEsd);

            // Row 5: Deployment (2 Columns)
            ModernGlowButton btnGenerateXml = CreateRegisterButton("Generate autounattend.xml", 6, 180, col2Width, 38, OnGenerateXml);
            ModernGlowButton btnCleanup = CreateRegisterButton("Force DISM Cleanup / Reset", 6 + col2Width + 12, 180, contentWidth - (col2Width + 12), 38, OnCleanupEnvironment);

            pnlTabDeployment.Controls.AddRange(new Control[] {
                btnBackupDrivers, btnRestoreDefault, btnRestoreBrowse,
                btnScanImages, btnBrowseImage, btnListWim, btnDeleteIndex,
                btnCompressFolder, btnWimToEsd, btnGenerateXml, btnCleanup
            });

            // Glowing Console Panel
            pnlDeploymentLogBorder = new ModernGlowConsolePanel
            {
                Location = new Point(6, 230),
                Size = new Size(pnlTabDeployment.Width - 12, pnlTabDeployment.Height - 238),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                IsDarkMode = isDarkMode,
                AccentColor = currentAccent
            };

            txtDeploymentLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10.5f, FontStyle.Regular)
            };
            txtDeploymentLog.GotFocus += (s, e) => pnlDeploymentLogBorder.IsConsoleFocused = true;
            txtDeploymentLog.LostFocus += (s, e) => pnlDeploymentLogBorder.IsConsoleFocused = false;

            pnlDeploymentLogBorder.Controls.Add(txtDeploymentLog);
            pnlTabDeployment.Controls.Add(pnlDeploymentLogBorder);
            this.Controls.Add(pnlTabDeployment);

            LogDeploy("Ready. Running as Administrator.");
            LogDeploy("Windows Deployment Suite v2.3 loaded (Deployment Hub, AppXPulse, AGC Solid).");
        }

        private void LogDeploy(string msg)
        {
            if (txtDeploymentLog.InvokeRequired) { txtDeploymentLog.Invoke(new Action(() => LogDeploy(msg))); return; }
            txtDeploymentLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }

        // ====================================================================
        // TAB 2: APPXPULSE
        // ====================================================================
        private void InitializeTabAppXPulse()
        {
            Color themeBg = isDarkMode ? Color.FromArgb(14, 16, 16) : Color.FromArgb(242, 244, 246);
            pnlTabAppXPulse = new Panel
            {
                Location = new Point(14, 56),
                Size = new Size(this.ClientSize.Width - 78, this.ClientSize.Height - 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = themeBg,
                Visible = false
            };

            int contentWidth = pnlTabAppXPulse.Width - 16;
            int half = (contentWidth - 12) / 2;

            ModernGlowButton btnScanInstalled = CreateRegisterButton("Scan Installed AppX Packages", 6, 4, half, 38, async (s, e) => await ScanAppXPackages(false));
            ModernGlowButton btnScanProvisioned = CreateRegisterButton("Scan Provisioned OS Packages (Image)", 6 + half + 12, 4, contentWidth - (half + 12), 38, async (s, e) => await ScanAppXPackages(true));
            ModernGlowButton btnRemoveSelected = CreateRegisterButton("Remove Selected Package", 6, 48, half, 38, OnRemoveSelectedAppX);
            ModernGlowButton btnClearList = CreateRegisterButton("Clear List", 6 + half + 12, 48, contentWidth - (half + 12), 38, (s, e) => lvAppX.Items.Clear());

            pnlTabAppXPulse.Controls.AddRange(new Control[] { btnScanInstalled, btnScanProvisioned, btnRemoveSelected, btnClearList });

            lvAppX = new ListView
            {
                Location = new Point(6, 94),
                Size = new Size(pnlTabAppXPulse.Width - 12, 180),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f)
            };
            lvAppX.Columns.Add("Package Name", 320);
            lvAppX.Columns.Add("Version", 140);
            lvAppX.Columns.Add("Type / Architecture", 140);
            lvAppX.Columns.Add("Full Package Identity", 400);
            pnlTabAppXPulse.Controls.Add(lvAppX);

            pnlAppXLogBorder = new ModernGlowConsolePanel
            {
                Location = new Point(6, 284),
                Size = new Size(pnlTabAppXPulse.Width - 12, pnlTabAppXPulse.Height - 292),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                IsDarkMode = isDarkMode,
                AccentColor = currentAccent
            };

            txtAppXLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10.5f, FontStyle.Regular)
            };
            txtAppXLog.GotFocus += (s, e) => pnlAppXLogBorder.IsConsoleFocused = true;
            txtAppXLog.LostFocus += (s, e) => pnlAppXLogBorder.IsConsoleFocused = false;

            pnlAppXLogBorder.Controls.Add(txtAppXLog);
            pnlTabAppXPulse.Controls.Add(pnlAppXLogBorder);
            this.Controls.Add(pnlTabAppXPulse);

            LogAppX("AppXPulse engine ready. Scan for user or provisioned image AppX packages.");
        }

        private void LogAppX(string msg)
        {
            if (txtAppXLog.InvokeRequired) { txtAppXLog.Invoke(new Action(() => LogAppX(msg))); return; }
            txtAppXLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }

        private async Task ScanAppXPackages(bool provisioned)
        {
            lvAppX.Items.Clear();
            LogAppX($"Scanning {(provisioned ? "Provisioned (OEM/Image)" : "Installed User")} packages...");

            await Task.Run(() =>
            {
                string cmd = provisioned ? "Get-AppxProvisionedPackage -Online | Select-Object DisplayName, Version, Architecture, PackageName"
                                         : "Get-AppxPackage | Select-Object Name, Version, Architecture, PackageFullName";

                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -Command \"" + cmd + "\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process? p = Process.Start(psi))
                {
                    if (p == null) return;
                    while (!p.StandardOutput.EndOfStream)
                    {
                        string? line = p.StandardOutput.ReadLine();
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("DisplayName") || line.StartsWith("----")) continue;

                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4)
                        {
                            this.Invoke(new Action(() =>
                            {
                                ListViewItem item = new ListViewItem(parts[0]);
                                item.SubItems.Add(parts[1]);
                                item.SubItems.Add(parts[2]);
                                item.SubItems.Add(parts[3]);
                                lvAppX.Items.Add(item);
                            }));
                        }
                    }
                    p.WaitForExit();
                }
            });

            LogAppX($"Found {lvAppX.Items.Count} packages.");
        }

        private void OnRemoveSelectedAppX(object? sender, EventArgs e)
        {
            if (lvAppX.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a package from the list to remove.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string pkg = lvAppX.SelectedItems[0].SubItems[3].Text;
            DialogResult confirm = MessageBox.Show($"Are you sure you want to remove package:\n\n{pkg}?", "Confirm Package Removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            LogAppX("Removing: " + pkg);
            Task.Run(() =>
            {
                RunCommand("powershell.exe", $"-NoProfile -Command \"Remove-AppxPackage -Package '{pkg}'\"");
                this.Invoke(new Action(() =>
                {
                    lvAppX.Items.Remove(lvAppX.SelectedItems[0]);
                    LogAppX("Package removal executed.");
                }));
            });
        }

        // ====================================================================
        // TAB 3: AGC SOLID ENGINE
        // ====================================================================
        private void InitializeTabAgc()
        {
            Color themeBg = isDarkMode ? Color.FromArgb(14, 16, 16) : Color.FromArgb(242, 244, 246);
            pnlTabAgc = new Panel
            {
                Location = new Point(14, 56),
                Size = new Size(this.ClientSize.Width - 78, this.ClientSize.Height - 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = themeBg,
                Visible = false,
                AllowDrop = true
            };
            pnlTabAgc.DragEnter += HandleAgcDragEnter;
            pnlTabAgc.DragDrop += HandleAgcDragDrop;

            lstAgcFiles = new ListBox
            {
                Location = new Point(6, 4),
                Size = new Size(pnlTabAgc.Width - 12, 110),
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = SelectionMode.MultiExtended,
                AllowDrop = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            lstAgcFiles.DragEnter += HandleAgcDragEnter;
            lstAgcFiles.DragDrop += HandleAgcDragDrop;
            lstAgcFiles.KeyDown += LstAgcFiles_KeyDown;
            pnlTabAgc.Controls.Add(lstAgcFiles);

            ModernGlowButton btnAddFiles = CreateRegisterButton("Add Files", 6, 120, 110, 36, BtnAgcAddFiles_Click);
            ModernGlowButton btnAddFolder = CreateRegisterButton("Add Folder", 124, 120, 110, 36, BtnAgcAddFolder_Click);
            ModernGlowButton btnClearQueue = CreateRegisterButton("Clear Queue", 242, 120, 110, 36, (s, e) => { lstAgcFiles.Items.Clear(); LogAgc("Queue cleared."); });
            ModernGlowButton btnSelectOutputDir = CreateRegisterButton("Output Dir...", 360, 120, 120, 36, BtnAgcSelectOutputDir_Click);

            lblAgcOutputDir = new Label
            {
                Text = $"Target path: {selectedOutputDirectory}",
                Location = new Point(490, 128),
                Size = new Size(pnlTabAgc.Width - 500, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            pnlTabAgc.Controls.AddRange(new Control[] { btnAddFiles, btnAddFolder, btnClearQueue, btnSelectOutputDir, lblAgcOutputDir });

            cmbAgcMethod = new ComboBox
            {
                Location = new Point(6, 166),
                Size = new Size(180, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            cmbAgcMethod.Items.AddRange(new object[] { "Store (No Compression)", "Fast (MSZIP)", "Ultra (LZMS Solid)" });
            cmbAgcMethod.SelectedIndex = 2;

            chkAgcSolid = new CheckBox { Text = "Solid archive", Location = new Point(195, 168), AutoSize = true, Checked = true };
            chkAgcTestArchive = new CheckBox { Text = "Test archive", Location = new Point(305, 168), AutoSize = true, Checked = true };
            chkAgcDeleteAfter = new CheckBox { Text = "Delete files after", Location = new Point(410, 168), AutoSize = true, Checked = false };

            btnAgcBackground = CreateRegisterButton("Priority: Normal", pnlTabAgc.Width - 156, 162, 150, 34, BtnAgcBackground_Click);
            btnAgcBackground.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            int halfAction = (pnlTabAgc.Width - 24) / 2;
            ModernGlowButton btnCompress = CreateRegisterButton("COMPRESS (.AGC)", 6, 202, halfAction, 38, async (s, e) => await HandleAgcCompressAsync());
            ModernGlowButton btnExtract = CreateRegisterButton("EXTRACT (.AGC)", 6 + halfAction + 12, 202, halfAction, 38, async (s, e) => await HandleAgcExtractAsync());

            pnlTabAgc.Controls.AddRange(new Control[] { cmbAgcMethod, chkAgcSolid, chkAgcTestArchive, chkAgcDeleteAfter, btnAgcBackground, btnCompress, btnExtract });

            lblAgcCurrentFile = new Label { Text = "Current Item: Idle", Location = new Point(6, 246), Size = new Size(pnlTabAgc.Width - 12, 18), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            prgAgcCurrent = new ProgressBar { Location = new Point(6, 266), Size = new Size(pnlTabAgc.Width - 12, 10), Style = ProgressBarStyle.Blocks, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            lblAgcTotal = new Label { Text = "Overall Progress: Ready", Location = new Point(6, 280), Size = new Size(pnlTabAgc.Width - 12, 18), Font = new Font("Segoe UI", 9F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            prgAgcTotal = new ProgressBar { Location = new Point(6, 300), Size = new Size(pnlTabAgc.Width - 12, 12), Style = ProgressBarStyle.Blocks, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            pnlTabAgc.Controls.AddRange(new Control[] { lblAgcCurrentFile, prgAgcCurrent, lblAgcTotal, prgAgcTotal });

            pnlAgcLogBorder = new ModernGlowConsolePanel
            {
                Location = new Point(6, 322),
                Size = new Size(pnlTabAgc.Width - 12, pnlTabAgc.Height - 330),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                IsDarkMode = isDarkMode,
                AccentColor = currentAccent
            };

            txtAgcLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10.5f, FontStyle.Regular)
            };
            txtAgcLog.GotFocus += (s, e) => pnlAgcLogBorder.IsConsoleFocused = true;
            txtAgcLog.LostFocus += (s, e) => pnlAgcLogBorder.IsConsoleFocused = false;

            pnlAgcLogBorder.Controls.Add(txtAgcLog);
            pnlTabAgc.Controls.Add(pnlAgcLogBorder);
            this.Controls.Add(pnlTabAgc);

            LogAgc("AGC Solid Engine initialized (cabinet.dll LZMS/MSZIP native bindings active). Ready for drag & drop.");
        }

        private void LogAgc(string msg)
        {
            if (txtAgcLog.InvokeRequired) { txtAgcLog.Invoke(new Action(() => LogAgc(msg))); return; }
            txtAgcLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }

        private void BtnAgcBackground_Click(object? sender, EventArgs e)
        {
            isAgcBackgroundMode = !isAgcBackgroundMode;
            btnAgcBackground.Text = isAgcBackgroundMode ? "Priority: Low (Bg)" : "Priority: Normal";
            if (activeWorkerThread != null && activeWorkerThread.IsAlive)
            {
                activeWorkerThread.Priority = isAgcBackgroundMode ? ThreadPriority.BelowNormal : ThreadPriority.Normal;
            }
            LogAgc($"AGC Engine Priority set to: {(isAgcBackgroundMode ? "Below Normal" : "Normal")}");
        }

        private void SetAgcCurrentProgress(int percent, string status)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => SetAgcCurrentProgress(percent, status))); return; }
            prgAgcCurrent.Style = ProgressBarStyle.Blocks;
            prgAgcCurrent.Value = Math.Clamp(percent, 0, 100);
            lblAgcCurrentFile.Text = status;
        }

        private void SetAgcTotalProgress(int percent, string status)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => SetAgcTotalProgress(percent, status))); return; }
            prgAgcTotal.Style = ProgressBarStyle.Blocks;
            prgAgcTotal.Value = Math.Clamp(percent, 0, 100);
            lblAgcTotal.Text = status;
        }

        private void HandleAgcDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void HandleAgcDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null) return;
            string[]? items = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (items == null) return;
            foreach (string item in items)
            {
                if (File.Exists(item)) { if (AddAgcFileItem(item, Path.GetFileName(item), false)) LogAgc($"Queued File: {Path.GetFileName(item)}"); }
                else if (Directory.Exists(item)) { QueueAgcDirectoryFiles(item); }
            }
        }

        private bool AddAgcFileItem(string fullPath, string relPath, bool isDir = false)
        {
            foreach (var ex in lstAgcFiles.Items)
            {
                if (ex is ArchiveItem ai && ai.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase)) return false;
            }
            lstAgcFiles.Items.Add(new ArchiveItem { FullPath = fullPath, RelativePath = relPath, IsDirectory = isDir });
            return true;
        }

        private void QueueAgcDirectoryFiles(string dirPath)
        {
            string? parent = Directory.GetParent(dirPath)?.FullName ?? dirPath;
            string[] files = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories);
            int count = 0;
            foreach (string f in files)
            {
                string rel = Path.GetRelativePath(parent, f);
                if (AddAgcFileItem(f, rel, false)) count++;
            }
            LogAgc($"Queued {count} files from: {Path.GetFileName(dirPath)}");
        }

        private void LstAgcFiles_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A) { for (int i = 0; i < lstAgcFiles.Items.Count; i++) lstAgcFiles.SetSelected(i, true); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Delete)
            {
                var sel = new List<object>();
                foreach (var itm in lstAgcFiles.SelectedItems) sel.Add(itm);
                foreach (var itm in sel) lstAgcFiles.Items.Remove(itm);
                e.SuppressKeyPress = true;
            }
        }

        private void BtnAgcAddFiles_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Multiselect = true, Title = "Select Files" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    int c = 0;
                    foreach (string f in ofd.FileNames) if (AddAgcFileItem(f, Path.GetFileName(f), false)) c++;
                    LogAgc($"Added {c} files.");
                }
            }
        }

        private void BtnAgcAddFolder_Click(object? sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK) QueueAgcDirectoryFiles(fbd.SelectedPath);
            }
        }

        private void BtnAgcSelectOutputDir_Click(object? sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    selectedOutputDirectory = fbd.SelectedPath;
                    lblAgcOutputDir.Text = $"Target path: {selectedOutputDirectory}";
                    LogAgc($"Output path: {selectedOutputDirectory}");
                }
            }
        }

        private async Task HandleAgcCompressAsync()
        {
            if (lstAgcFiles.Items.Count == 0) { MessageBox.Show("Please add files first.", "Empty Queue", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Alice/Gemini Archive (*.agc)|*.agc", DefaultExt = "agc", FileName = "archive.agc", InitialDirectory = selectedOutputDirectory })
            {
                if (sfd.ShowDialog() != DialogResult.OK) return;
                string savePath = sfd.FileName;

                List<ArchiveItem> items = new List<ArchiveItem>();
                foreach (var itm in lstAgcFiles.Items) if (itm is ArchiveItem ai) items.Add(ai);

                int selectedMethod = cmbAgcMethod.SelectedIndex;
                bool testArchive = chkAgcTestArchive.Checked;
                bool deleteAfter = chkAgcDeleteAfter.Checked;

                try
                {
                    await Task.Run(() =>
                    {
                        activeWorkerThread = Thread.CurrentThread;
                        if (isAgcBackgroundMode) activeWorkerThread.Priority = ThreadPriority.BelowNormal;

                        LogAgc("Assembling payload structure...");
                        long totalBytes = 0;
                        foreach (var itm in items) if (!itm.IsDirectory && File.Exists(itm.FullPath)) totalBytes += new FileInfo(itm.FullPath).Length;

                        long processed = 0;
                        byte[] rawData;

                        using (MemoryStream uncompressed = new MemoryStream())
                        using (BinaryWriter writer = new BinaryWriter(uncompressed, Encoding.UTF8))
                        {
                            writer.Write(items.Count);
                            for (int i = 0; i < items.Count; i++)
                            {
                                var item = items[i];
                                if (item.IsDirectory) { writer.Write(item.RelativePath); writer.Write(-1); continue; }
                                if (!File.Exists(item.FullPath)) continue;

                                byte[] fileBytes = File.ReadAllBytes(item.FullPath);
                                writer.Write(item.RelativePath);
                                writer.Write(fileBytes.Length);
                                writer.Write(fileBytes);

                                processed += fileBytes.Length;
                                int percent = totalBytes > 0 ? (int)((double)processed / totalBytes * 100) : 100;
                                SetAgcCurrentProgress(100, $"Packed: {item.RelativePath}");
                                SetAgcTotalProgress(percent, $"Packing: {percent}%");
                            }
                            rawData = uncompressed.ToArray();
                        }

                        byte[] finalPayload;
                        uint methodUsed = 0;

                        if (selectedMethod == 0)
                        {
                            finalPayload = rawData;
                            methodUsed = 0;
                        }
                        else
                        {
                            uint algo = selectedMethod == 1 ? COMPRESS_ALGORITHM_MSZIP : COMPRESS_ALGORITHM_LZMS;
                            methodUsed = algo;
                            string name = selectedMethod == 1 ? "MSZIP" : "LZMS";
                            LogAgc($"Compressing solid block using native {name} engine...");

                            if (!CreateCompressor(algo, IntPtr.Zero, out IntPtr compressor))
                                throw new InvalidOperationException("Failed to initialize compressor.");

                            try
                            {
                                UIntPtr uncompressedSize = new UIntPtr((ulong)rawData.Length);
                                Compress(compressor, rawData, uncompressedSize, null, UIntPtr.Zero, out UIntPtr req);
                                byte[] compressedBuffer = new byte[(int)req];
                                Compress(compressor, rawData, uncompressedSize, compressedBuffer, req, out UIntPtr actual);
                                finalPayload = new byte[(int)actual];
                                Buffer.BlockCopy(compressedBuffer, 0, finalPayload, 0, (int)actual);
                            }
                            finally { CloseCompressor(compressor); }
                        }

                        using (FileStream fs = new FileStream(savePath, FileMode.Create))
                        using (BinaryWriter archiver = new BinaryWriter(fs))
                        {
                            archiver.Write(Encoding.ASCII.GetBytes("AGC!"));
                            archiver.Write(methodUsed);
                            archiver.Write(items.Count);
                            archiver.Write(rawData.Length);
                            archiver.Write(finalPayload.Length);
                            archiver.Write(finalPayload);
                        }

                        LogAgc($"SUCCESS: Created {Path.GetFileName(savePath)} [{(finalPayload.Length / 1024.0 / 1024.0):F2} MB]");

                        if (testArchive && methodUsed != 0)
                        {
                            LogAgc("Running self-test verification pass...");
                            if (CreateDecompressor(methodUsed, IntPtr.Zero, out IntPtr decompressor))
                            {
                                try
                                {
                                    byte[] testBuf = new byte[rawData.Length];
                                    Decompress(decompressor, finalPayload, (UIntPtr)finalPayload.Length, testBuf, (UIntPtr)testBuf.Length, out UIntPtr dec);
                                    if ((int)dec == rawData.Length) LogAgc("Verification passed: 100% OK.");
                                }
                                finally { CloseDecompressor(decompressor); }
                            }
                        }

                        if (deleteAfter)
                        {
                            foreach (var itm in items) if (!itm.IsDirectory && File.Exists(itm.FullPath)) File.Delete(itm.FullPath);
                            LogAgc("Original files removed.");
                        }
                    });

                    SetAgcCurrentProgress(100, "Done.");
                    SetAgcTotalProgress(100, "Archive complete.");
                }
                catch (Exception ex)
                {
                    LogAgc("ERROR: " + ex.Message);
                }
                finally { activeWorkerThread = null; }
            }
        }

        private async Task HandleAgcExtractAsync()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Alice/Gemini Archive|*.agc", InitialDirectory = selectedOutputDirectory })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;
                string archivePath = ofd.FileName;
                string targetDir = selectedOutputDirectory;

                try
                {
                    await Task.Run(() =>
                    {
                        activeWorkerThread = Thread.CurrentThread;
                        if (isAgcBackgroundMode) activeWorkerThread.Priority = ThreadPriority.BelowNormal;

                        LogAgc($"Opening archive: {Path.GetFileName(archivePath)}");
                        byte[] compressedPayload;
                        int origSize;
                        uint methodUsed = 5;

                        using (FileStream fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
                        using (BinaryReader header = new BinaryReader(fs))
                        {
                            byte[] magic = header.ReadBytes(4);
                            if (Encoding.ASCII.GetString(magic) != "AGC!") throw new InvalidDataException("Not an AGC archive.");
                            methodUsed = header.ReadUInt32();
                            if (methodUsed > 10) methodUsed = COMPRESS_ALGORITHM_LZMS;
                            else header.ReadInt32();
                            origSize = header.ReadInt32();
                            int compSize = header.ReadInt32();
                            compressedPayload = header.ReadBytes(compSize);
                        }

                        byte[] decompressed;
                        if (methodUsed == 0) { decompressed = compressedPayload; }
                        else
                        {
                            LogAgc("Executing native decompression...");
                            if (!CreateDecompressor(methodUsed, IntPtr.Zero, out IntPtr decompressor))
                                throw new InvalidOperationException("Decompressor failed to init.");

                            decompressed = new byte[origSize];
                            try
                            {
                                Decompress(decompressor, compressedPayload, (UIntPtr)compressedPayload.Length, decompressed, (UIntPtr)decompressed.Length, out UIntPtr raw);
                            }
                            finally { CloseDecompressor(decompressor); }
                        }

                        using (MemoryStream ms = new MemoryStream(decompressed))
                        using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8))
                        {
                            int count = reader.ReadInt32();
                            for (int i = 0; i < count; i++)
                            {
                                string rel = reader.ReadString().TrimStart('\\', '/');
                                int len = reader.ReadInt32();
                                string outP = Path.GetFullPath(Path.Combine(targetDir, rel));

                                if (len == -1)
                                {
                                    if (!Directory.Exists(outP)) Directory.CreateDirectory(outP);
                                }
                                else
                                {
                                    byte[] data = reader.ReadBytes(len);
                                    string? d = Path.GetDirectoryName(outP);
                                    if (!string.IsNullOrEmpty(d) && !Directory.Exists(d)) Directory.CreateDirectory(d);
                                    File.WriteAllBytes(outP, data);
                                    LogAgc($"Extracted: {rel}");
                                }
                                int p = (int)((double)(i + 1) / count * 100);
                                SetAgcTotalProgress(p, $"Extracted: {p}%");
                            }
                        }
                        LogAgc("Extraction finalized successfully.");
                    });
                    SetAgcTotalProgress(100, "Done.");
                }
                catch (Exception ex)
                {
                    LogAgc("ERROR: " + ex.Message);
                }
                finally { activeWorkerThread = null; }
            }
        }

        // ====================================================================
        // THEME & DISM UTILITY METHODS
        // ====================================================================
        private void SetDarkMode(bool dark)
        {
            isDarkMode = dark;
            Color bg = dark ? Color.FromArgb(14, 16, 16) : Color.FromArgb(242, 244, 246);
            this.BackColor = bg;
            lblCopyright.ForeColor = dark ? Color.FromArgb(120, 130, 130) : Color.FromArgb(130, 140, 145);

            pnlTabDeployment.BackColor = bg;
            pnlTabAppXPulse.BackColor = bg;
            pnlTabAgc.BackColor = bg;

            tabStrip.IsDarkMode = dark;
            tabStrip.Invalidate();

            paletteBar.IsDarkMode = dark;
            paletteBar.Invalidate();

            toggleMode.IsDarkMode = dark;
            toggleMode.Invalidate();

            foreach (var btn in registeredButtons)
            {
                btn.IsDarkMode = dark;
                btn.Invalidate();
            }

            pnlDeploymentLogBorder.IsDarkMode = dark;
            txtDeploymentLog.BackColor = dark ? Color.FromArgb(8, 10, 12) : Color.FromArgb(254, 255, 255);
            pnlDeploymentLogBorder.Invalidate();

            pnlAppXLogBorder.IsDarkMode = dark;
            txtAppXLog.BackColor = dark ? Color.FromArgb(8, 10, 12) : Color.FromArgb(254, 255, 255);
            lvAppX.BackColor = dark ? Color.FromArgb(18, 22, 26) : Color.FromArgb(255, 255, 255);
            lvAppX.ForeColor = dark ? Color.FromArgb(235, 240, 245) : Color.FromArgb(20, 25, 30);
            pnlAppXLogBorder.Invalidate();

            pnlAgcLogBorder.IsDarkMode = dark;
            txtAgcLog.BackColor = dark ? Color.FromArgb(8, 10, 12) : Color.FromArgb(254, 255, 255);
            lstAgcFiles.BackColor = dark ? Color.FromArgb(18, 22, 26) : Color.FromArgb(255, 255, 255);
            lstAgcFiles.ForeColor = dark ? Color.FromArgb(235, 240, 245) : Color.FromArgb(20, 25, 30);
            cmbAgcMethod.BackColor = dark ? Color.FromArgb(24, 28, 32) : Color.FromArgb(240, 244, 248);
            cmbAgcMethod.ForeColor = dark ? Color.White : Color.Black;
            chkAgcSolid.ForeColor = dark ? Color.White : Color.Black;
            chkAgcTestArchive.ForeColor = dark ? Color.White : Color.Black;
            chkAgcDeleteAfter.ForeColor = dark ? Color.White : Color.Black;
            lblAgcCurrentFile.ForeColor = dark ? Color.White : Color.Black;
            lblAgcTotal.ForeColor = dark ? Color.White : Color.Black;
            lblAgcOutputDir.ForeColor = dark ? Color.FromArgb(150, 160, 165) : Color.FromArgb(100, 110, 115);
            pnlAgcLogBorder.Invalidate();
        }

        private void ApplyAccentTheme(Color newAccent)
        {
            currentAccent = newAccent;
            tabStrip.AccentColor = newAccent;
            tabStrip.Invalidate();

            toggleMode.AccentColor = newAccent;
            foreach (var btn in registeredButtons) btn.AccentColor = newAccent;

            pnlDeploymentLogBorder.AccentColor = newAccent;
            txtDeploymentLog.ForeColor = isDarkMode ? newAccent : ControlPaint.Dark(newAccent, 0.45f);

            pnlAppXLogBorder.AccentColor = newAccent;
            txtAppXLog.ForeColor = isDarkMode ? newAccent : ControlPaint.Dark(newAccent, 0.45f);

            pnlAgcLogBorder.AccentColor = newAccent;
            txtAgcLog.ForeColor = isDarkMode ? newAccent : ControlPaint.Dark(newAccent, 0.45f);
        }

        private static string GetConfigPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.cfg");
        private static string GetModeConfigPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mode.cfg");

        public static Color LoadSavedAccent()
        {
            try { string path = GetConfigPath(); if (File.Exists(path)) return ColorTranslator.FromHtml(File.ReadAllText(path).Trim()); } catch { }
            return Palettes[0];
        }

        private static void SaveAccentPreference(Color c)
        {
            try { File.WriteAllText(GetConfigPath(), ColorTranslator.ToHtml(c)); } catch { }
        }

        public static bool LoadSavedThemeMode()
        {
            try { string path = GetModeConfigPath(); if (File.Exists(path)) return File.ReadAllText(path).Trim().Equals("dark", StringComparison.OrdinalIgnoreCase); } catch { }
            return true;
        }

        private static void SaveThemeModePreference(bool dark)
        {
            try { File.WriteAllText(GetModeConfigPath(), dark ? "dark" : "light"); } catch { }
        }

        private void OnListWimInfo(object? sender, EventArgs e)
        {
            string? img = PickImageFile();
            if (string.IsNullOrEmpty(img)) return;
            LogDeploy("Querying editions inside: " + Path.GetFileName(img));
            RunCommand("dism.exe", "/Get-WimInfo /WimFile:\"" + img + "\"");
        }

        private void OnDeleteWimIndex(object? sender, EventArgs e)
        {
            string? img = PickImageFile();
            if (string.IsNullOrEmpty(img)) return;
            RunCommand("dism.exe", "/Get-WimInfo /WimFile:\"" + img + "\"");
            string idx = ShowInputDialog("Delete Index", "Enter index to delete:", "");
            if (string.IsNullOrEmpty(idx)) return;
            RunCommand("dism.exe", "/Delete-Image /ImageFile:\"" + img + "\" /Index:" + idx + " /CheckIntegrity");
        }

        private string? PickImageFile()
        {
            string defaultWim = Path.Combine(workDir, "install.wim");
            if (File.Exists(defaultWim)) return defaultWim;
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Windows Images (*.wim;*.esd)|*.wim;*.esd" })
            {
                if (ofd.ShowDialog() == DialogResult.OK) return ofd.FileName;
            }
            return null;
        }

        private void OnBackupDrivers(object? sender, EventArgs e)
        {
            LogDeploy("Exporting drivers to: " + driverBackupDir);
            if (!Directory.Exists(driverBackupDir)) Directory.CreateDirectory(driverBackupDir);
            RunCommand("dism.exe", "/online /export-driver /destination:\"" + driverBackupDir + "\"");
        }

        private void OnRestoreDriversDefault(object? sender, EventArgs e) => InstallDriversFromFolder(driverBackupDir);

        private void OnRestoreDriversBrowse(object? sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK) InstallDriversFromFolder(fbd.SelectedPath);
            }
        }

        private void InstallDriversFromFolder(string folder)
        {
            if (!Directory.Exists(folder)) { LogDeploy("Folder missing: " + folder); return; }
            LogDeploy("Installing drivers from: " + folder);
            RunCommand("pnputil.exe", "/add-driver \"" + folder + "\\*.inf\" /subdirs /install");
        }

        private void OnScanAndSlipstream(object? sender, EventArgs e)
        {
            string[] found = Directory.GetFiles(workDir, "*.*", SearchOption.AllDirectories);
            string? target = null;
            foreach (string file in found)
            {
                string name = Path.GetFileName(file).ToLower();
                if (name == "install.wim" || name == "install.esd") { target = file; break; }
            }
            if (target != null) ProcessSlipstream(target);
            else LogDeploy("No install.wim/esd found in current workspace.");
        }

        private void OnBrowseAndSlipstream(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Windows Images (*.wim;*.esd)|*.wim;*.esd" })
            {
                if (ofd.ShowDialog() == DialogResult.OK) ProcessSlipstream(ofd.FileName);
            }
        }

        private void ProcessSlipstream(string imgPath)
        {
            LogDeploy("Selected: " + imgPath);
            string ext = Path.GetExtension(imgPath).ToLower();
            RunCommand("dism.exe", "/Get-WimInfo /WimFile:\"" + imgPath + "\"");
            string idx = ShowInputDialog("Image Index", "Enter index to patch:", "1");
            if (string.IsNullOrEmpty(idx)) return;

            string targetWim = imgPath;
            if (ext == ".esd")
            {
                if (File.Exists(tempWim)) File.Delete(tempWim);
                RunCommand("dism.exe", "/Export-Image /SourceImageFile:\"" + imgPath + "\" /SourceIndex:" + idx + " /DestinationImageFile:\"" + tempWim + "\" /Compress:fast");
                targetWim = tempWim;
                idx = "1";
            }

            if (Directory.Exists(mountDir)) Directory.Delete(mountDir, true);
            Directory.CreateDirectory(mountDir);

            LogDeploy("Mounting image...");
            RunCommand("dism.exe", "/Mount-Image /ImageFile:\"" + targetWim + "\" /Index:" + idx + " /MountDir:\"" + mountDir + "\"");
            LogDeploy("Injecting drivers...");
            RunCommand("dism.exe", "/Image:\"" + mountDir + "\" /Add-Driver /Driver:\"" + driverBackupDir + "\" /Recurse");
            LogDeploy("Unmounting and committing...");
            RunCommand("dism.exe", "/Unmount-Image /MountDir:\"" + mountDir + "\" /Commit");

            if (ext == ".esd")
            {
                string bak = imgPath + ".bak";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(imgPath, bak);
                RunCommand("dism.exe", "/Export-Image /SourceImageFile:\"" + tempWim + "\" /SourceIndex:1 /DestinationImageFile:\"" + imgPath + "\" /Compress:recovery");
                if (File.Exists(tempWim)) File.Delete(tempWim);
            }
            LogDeploy("SUCCESS: Driver injection complete!");
        }

        private void OnCompressFolderPrompt(object? sender, EventArgs e)
        {
            string srcFolder = "";
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (Directory.Exists(driverBackupDir)) fbd.SelectedPath = driverBackupDir;
                if (fbd.ShowDialog() != DialogResult.OK) return;
                srcFolder = fbd.SelectedPath;
            }

            string targetZip = Path.Combine(outDir, "Compressed_Drivers.zip");
            try
            {
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                if (File.Exists(targetZip)) File.Delete(targetZip);
                ZipFile.CreateFromDirectory(srcFolder, targetZip, CompressionLevel.Optimal, false);
                LogDeploy("SUCCESS: Created " + targetZip);
            }
            catch (Exception ex) { LogDeploy("ERROR: " + ex.Message); }
        }

        private void OnConvertWimToEsd(object? sender, EventArgs e)
        {
            string srcWim = "";
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "WIM Image (*.wim)|*.wim" })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;
                srcWim = ofd.FileName;
            }

            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            string destEsd = Path.Combine(outDir, "install.esd");
            if (File.Exists(destEsd)) File.Delete(destEsd);

            int imgCount = GetWimIndexCount(srcWim);
            for (int i = 1; i <= imgCount; i++)
            {
                LogDeploy($"Compressing Index {i} of {imgCount}...");
                RunCommand("dism.exe", $"/Export-Image /SourceImageFile:\"{srcWim}\" /SourceIndex:{i} /DestinationImageFile:\"{destEsd}\" /Compress:recovery /CheckIntegrity");
            }
            LogDeploy("SUCCESS: Created " + destEsd);
        }

        private int GetWimIndexCount(string wimPath)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("dism.exe", "/Get-WimInfo /WimFile:\"" + wimPath + "\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                int count = 0;
                using (Process? p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        while (!p.StandardOutput.EndOfStream)
                        {
                            string? l = p.StandardOutput.ReadLine();
                            if (l != null && l.Contains("Index :")) count++;
                        }
                        p.WaitForExit();
                    }
                }
                return count;
            }
            catch { return 1; }
        }

        private void OnGenerateXml(object? sender, EventArgs e)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string xmlPath = Path.Combine(desktop, "autounattend.xml");
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">\n" +
                "  <settings pass=\"windowsPE\">\n    <component name=\"Microsoft-Windows-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" xmlns:wcm=\"http://microsoft.com\" xmlns:xsi=\"http://www.w3.org\">\n" +
                "      <UserData><AcceptEula>true</AcceptEula></UserData>\n      <RunSynchronous>\n        <RunSynchronousCommand wcm:action=\"add\">\n          <Order>1</Order>\n          <Description>Bypass Requirements</Description>\n" +
                "          <Path>cmd /c reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassTPMCheck /t REG_DWORD /d 1 /f &amp;&amp; reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassSecureBootCheck /t REG_DWORD /d 1 /f &amp;&amp; reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassRAMCheck /t REG_DWORD /d 1 /f &amp;&amp; reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassCPUCheck /t REG_DWORD /d 1 /f</Path>\n" +
                "        </RunSynchronousCommand>\n      </RunSynchronous>\n    </component>\n  </settings>\n</unattend>";

            try { File.WriteAllText(xmlPath, xml); LogDeploy("SUCCESS: Created " + xmlPath); }
            catch (Exception ex) { LogDeploy("ERROR: " + ex.Message); }
        }

        private void OnCleanupEnvironment(object? sender, EventArgs e)
        {
            LogDeploy("Executing DISM cleanup...");
            RunCommand("dism.exe", "/Cleanup-Mountpoints");
            RunCommand("dism.exe", "/Cleanup-Wim");
            if (Directory.Exists(mountDir)) try { Directory.Delete(mountDir, true); } catch { }
            if (File.Exists(tempWim)) try { File.Delete(tempWim); } catch { }
            LogDeploy("Cleaned.");
        }

        private int RunCommand(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process? p = Process.Start(psi))
                {
                    if (p == null) return -1;
                    while (!p.StandardOutput.EndOfStream)
                    {
                        string? l = p.StandardOutput.ReadLine();
                        if (!string.IsNullOrWhiteSpace(l)) LogDeploy(l);
                    }
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }
            catch (Exception ex) { LogDeploy("EXEC ERROR: " + ex.Message); return -1; }
        }

        private string ShowInputDialog(string caption, string prompt, string defaultVal)
        {
            using (Form form = new Form())
            {
                form.Width = 380; form.Height = 175;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.Text = caption; form.StartPosition = FormStartPosition.CenterParent;
                form.BackColor = isDarkMode ? Color.FromArgb(40, 40, 40) : Color.FromArgb(245, 245, 245);
                form.ForeColor = isDarkMode ? Color.White : Color.Black;

                Label lbl = new Label { Left = 20, Top = 15, Text = prompt, AutoSize = true, Font = new Font("Segoe UI", 10f) };
                TextBox box = new TextBox { Left = 20, Top = 45, Width = 320, Text = defaultVal, Font = new Font("Segoe UI", 10.5f) };
                Button btnOk = new Button { Text = "OK", Left = 170, Width = 80, Top = 85, Height = 32, DialogResult = DialogResult.OK };
                Button btnCancel = new Button { Text = "Cancel", Left = 260, Width = 80, Top = 85, Height = 32, DialogResult = DialogResult.Cancel };

                form.Controls.AddRange(new Control[] { lbl, box, btnOk, btnCancel });
                form.AcceptButton = btnOk; form.CancelButton = btnCancel;
                return form.ShowDialog() == DialogResult.OK ? box.Text : "";
            }
        }

        [STAThread]
        public static void Main()
        {
            bool isElevated = false;
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(id);
                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            if (!isElevated)
            {
                ProcessStartInfo procInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = Application.ExecutablePath!,
                    Verb = "runas"
                };
                try { Process.Start(procInfo); Environment.Exit(0); return; }
                catch { MessageBox.Show("Administrator privileges required.", "Permission Required", MessageBoxButtons.OK, MessageBoxIcon.Stop); Environment.Exit(1); return; }
            }

            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }
}