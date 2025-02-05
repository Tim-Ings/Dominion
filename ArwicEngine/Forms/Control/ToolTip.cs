﻿// Dominion - Copyright (C) Timothy Ings
// ToolTip.cs
// This file contains classes that define a tool tip

using ArwicEngine.Core;
using ArwicEngine.Graphics;
using ArwicEngine.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using static ArwicEngine.Constants;

namespace ArwicEngine.Forms
{
    public class ToolTip : Control
    {
        #region Defaults
        public static Sprite DefaultSprite;

        public static new void InitDefaults()
        {
            DefaultSprite = Engine.Instance.Content.GetAsset<Sprite>(ASSET_CONTROL_FORM_BACK);
        }
        #endregion

        #region Properties & Accessors
        /// <summary>
        /// Gets or sets the text associated with this control
        /// </summary>
        public new RichText Text
        {
            get
            {
                return _text;
            }
            set
            {
                RichText last = _text;
                _text = value;
                if (last != _text)
                    OnTextChanged(EventArgs.Empty);
            }
        }
        private RichText _text;
        /// <summary>
        /// Gets or sets a value that indicate whether the tooltip follows the cursor
        /// </summary>
        public bool FollowCurosr
        {
            get
            {
                return _followCursor;
            }
            set
            {
                bool last = _followCursor;
                _followCursor = value;
                if (last != _followCursor)
                    OnFollowCursorChanged(EventArgs.Empty);
            }
        }
        private bool _followCursor;
        /// <summary>
        /// Gets or sets the value that is used as an offset when rendering the tooltip at the cursor
        /// </summary>
        public Point CursorOffset { get; set; }
        /// <summary>
        /// Gets or sets the sprite used to draw the background of the tooltip
        /// </summary>
        public Sprite Sprite { get; set; }
        private RichText[] wrappedText;
        private int padding;
        private int lineSpacing;
        private int lineHeight;
        private int maxLineWidth;
        #endregion

        #region Events
        public event EventHandler FollowCursorChanged;
        #endregion

        #region Event Handlers
        protected virtual void OnFollowCursorChanged(EventArgs args)
        {
            if (FollowCursorChanged != null)
                FollowCursorChanged(this, args);
        }
        #endregion

        /// <summary>
        /// Initializes a new instance of the ToolTip class with default settings
        /// </summary>
        /// <param name="pos">Position of the Button</param>
        /// <param name="parent">required parent</param>
        public ToolTip(RichText text, int width)
            : base(new Rectangle(0, 0, width, 0), null)
        {
            Sprite = DefaultSprite;
            Visible = false;
            padding = 10;
            lineSpacing = 5;
            FollowCurosr = true;
            CursorOffset = new Point(20, 20);
            MouseMove += ToolTip_MouseMove;
            TextChanged += ToolTip_TextChanged;
            FontChanged += ToolTip_FontChanged;
            Font = DefaultFont;
            Text = text;
        }
        
        private void ToolTip_FontChanged(object sender, EventArgs e)
        {
            UpdateLines();
        }
        private void ToolTip_TextChanged(object sender, EventArgs e)
        {
            UpdateLines();
        }
        private void ToolTip_MouseMove(object sender, MouseEventArgs e)
        {
            if (Parent.Visible && Parent.AbsoluteBounds.Contains(e.Position))
            {
                Visible = true;
                if (FollowCurosr)
                    AbsoluteLocation = e.Position + CursorOffset;
            }
            else
            {
                Visible = false;
            }
        }

        private void UpdateLines()
        {
            wrappedText = Font.WordWrap(Text, Size.Width - padding * 2).ToArray();
            foreach (RichText line in wrappedText)
            {
                Vector2 measure = line.Measure();

                int height = (int)measure.Y;
                if (height > lineHeight)
                    lineHeight = height;

                int width = (int)measure.X;
                if (width > maxLineWidth)
                    maxLineWidth = width;
            }
        }

        public override bool Update()
        {
            if (Parent != null)
            {
                Visible = Parent.AbsoluteBounds.Contains(InputManager.Instance.MouseScreenPos());

                if (Visible)
                {
                    if (FollowCurosr)
                    {
                        AbsoluteBounds = new Rectangle(
                            CursorOffset.X + InputManager.Instance.MouseScreenPos().X,
                            CursorOffset.Y + InputManager.Instance.MouseScreenPos().Y,
                            maxLineWidth + padding * 2,
                            (2 * padding + (lineSpacing + lineHeight) * wrappedText.Length - 1));
                    }
                    else
                    {
                        Bounds = new Rectangle(
                            Location.X,
                            Location.Y,
                            maxLineWidth + padding * 2,
                            (2 * padding + (lineSpacing + lineHeight) * wrappedText.Length - 1));
                    }
                }
            }

            return base.Update();
        }

        public override void Draw(SpriteBatch sb)
        {
            if (Visible)
            {
                if (AbsoluteLocation.X + Size.Width > GraphicsManager.Instance.Viewport.Width)
                {
                    AbsoluteLocation = new Point(AbsoluteLocation.X - Size.Width, AbsoluteLocation.Y);
                }
                Sprite.DrawNineCut(sb, AbsoluteBounds, null, Color);
                for (int i = 0; i < wrappedText.Length; i++)
                {
                    wrappedText[i].Draw(sb, AbsoluteLocation.ToVector2() + new Vector2(padding, padding + (lineSpacing + lineHeight) * i));
                }
            }
            base.Draw(sb);
        }
    }
}
