﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;

using System;

using Apollo.Enums;

namespace Apollo.Enums
{
    public enum FadeTypeEnum
    {
        Linear = 0,
        Smooth = 1,
        Fast = 2,
        Slow = 3,
        Hold = 4
    };
}



namespace Apollo.Components
{
    public class FadeThumb : UserControl
    {
        void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            Base = this.Get<Thumb>("Thumb");
        }

        public delegate void MovedEventHandler(FadeThumb sender, double change, double? total);
        public event MovedEventHandler Moved;

        public delegate void FadeThumbEventHandler(FadeThumb sender);
        public event FadeThumbEventHandler Focused;
        public event FadeThumbEventHandler Deleted;
        public event FadeThumbEventHandler MenuOpened;
        public event FadeThumbEventHandler FadeTypeChanged;
        public FadeTypeEnum FadeType = FadeTypeEnum.Linear;
        ContextMenu ThumbContextMenu;
        public Thumb Base;

        public IBrush Fill
        {
            get => (IBrush)this.Resources["Color"];
            set => this.Resources["Color"] = value;
        }

        public FadeThumb()
        {
            InitializeComponent();

            ThumbContextMenu = (ContextMenu)this.Resources["ThumbContextMenu"];

            ThumbContextMenu.AddHandler(MenuItem.ClickEvent, ContextMenu_Click);

            UpdateSelectedMenu(FadeTypeEnum.Linear);

            Base.AddHandler(InputElement.PointerPressedEvent, MouseDown, RoutingStrategies.Tunnel);
            Base.AddHandler(InputElement.PointerReleasedEvent, MouseUp, RoutingStrategies.Tunnel);

        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e)
        {
            Moved = null;
            Focused = null;
            Deleted = null;

            Base.RemoveHandler(InputElement.PointerPressedEvent, MouseDown);
            Base.RemoveHandler(InputElement.PointerReleasedEvent, MouseUp);
        }

        bool dragged = false;

        void DragStarted(object sender, VectorEventArgs e)
        {
            ((Window)this.GetVisualRoot()).Focus();
            dragged = false;
        }

        void DragCompleted(object sender, VectorEventArgs e)
        {
            if (!dragged) Focused?.Invoke(this);
            else if (change != 0) Moved?.Invoke(this, 0, change);
        }

        void MouseDown(object sender, PointerPressedEventArgs e)
        {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton != PointerUpdateKind.LeftButtonPressed) e.Handled = true;

            ((Window)this.GetVisualRoot()).Focus();
        }

        void MouseUp(object sender, PointerReleasedEventArgs e)
        {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.RightButtonReleased)
            {
                MenuOpened?.Invoke(this);
                e.Handled = true;
            }
        }

        double change;

        void MouseMove(object sender, VectorEventArgs e)
        {
            if (!dragged) change = 0;
            change += e.Vector.X;

            dragged = true;
            Moved?.Invoke(this, e.Vector.X, null);
        }

        public void Select() => this.Resources["Outline"] = new SolidColorBrush(new Color(255, 255, 255, 255));
        public void Unselect() => this.Resources["Outline"] = new SolidColorBrush(new Color(0, 255, 255, 255));

        public void OpenMenu()
        {
            ThumbContextMenu.Open(Base);
        }
        public void ContextMenu_Click(object sender, EventArgs e)
        {
            IInteractive item = ((RoutedEventArgs)e).Source;

            if (item.GetType() == typeof(MenuItem))
            {
                MenuItem selectedItem = (MenuItem)item;
                Enum.TryParse(selectedItem.Header.ToString(), out FadeType);
                UpdateSelectedMenu(FadeType);
                FadeTypeChanged?.Invoke(this);
            }
        }

        public void UpdateSelectedMenu(FadeTypeEnum type)
        {
            MenuItem selectedMenu = null;
            foreach (MenuItem item in ThumbContextMenu.Items)
            {
                //TODO Fix crash on redo as menu hasn't been loaded yet 
                item.Icon = "";
                if (item.Header.ToString() == type.ToString())
                {
                    selectedMenu = item;
                }
            }
            selectedMenu.Icon = this.Resources["SeletedIcon"];
        }

        public MenuItem GetMenuItem(FadeTypeEnum type)
        {
            foreach (MenuItem item in ThumbContextMenu.Items)
            {
                if (item.Header.ToString() == type.ToString()) return item;
            }
            return null;
        }
    }
}
