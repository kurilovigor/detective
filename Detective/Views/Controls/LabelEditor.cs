using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
// Resolve ambiguous types introduced by System.Drawing/System.Windows.Forms global usings
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;
using Rectangle = System.Windows.Shapes.Rectangle;
using Image = System.Windows.Controls.Image;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Detective.ViewModels;

namespace Detective.Views.Controls
{

    /// <summary>
    /// A Canvas-based editor that allows placing, moving and resizing labeled rectangles over an optional centered background image.
    /// </summary>
    public class LabelEditor : Canvas
    {
        private const double CornerHitRadius = 5.0;

        private Image _backgroundImage;
        private readonly List<Rectangle> _visuals = new();
        private readonly Dictionary<Rectangle, Rectangle[]> _grips = new();
        private Point _mouseStart;
        private Rectangle? _activeVisual;
        private Rectangle? _hoveredVisual;
        private bool _isDrawing;
        private bool _isMoving;
        private bool _isResizing;
        private Corner _resizeCorner;
        private Point _moveStartOffset;

        // replaced by ActionsHistory
        private ActionsHistory _history = new ActionsHistory();

        // Operation tracking
        private Guid _operationTargetId = Guid.Empty;
        private Rect _operationOriginalRect;

        // Currently selected model
        private LabelRect? _selected;

        /// <summary>
        /// Raised when the Selected LabelRect changes.
        /// </summary>
        public event EventHandler<LabelRect?>? SelectedChanged;

        /// <summary>
        /// Currently selected model
        /// </summary>
        public LabelRect? Selected
        {
            get => _selected;
            set
            {
                if (ReferenceEquals(_selected, value)) return;
                _selected = value;
                // update visuals when selection changes
                UpdateVisualSelectionStyles();
                ShowGripsFor(_visuals.FirstOrDefault(v => (v.Tag is Guid id) && _selected != null && id == _selected.Id));
                SelectedChanged?.Invoke(this, _selected);
            }
        }

        public ObservableCollection<LabelRect> Rectangles { get; } = new();

        // Delete selected rectangle or programmatically remove
        public void DeleteSelected()
        {
            if (Selected == null) return;
            var item = Selected;
            // remove from collection
            int idx = -1;
            for (int i = 0; i < Rectangles.Count; i++)
            {
                if (Rectangles[i].Id == item.Id) { idx = i; break; }
            }
            if (idx >= 0) Rectangles.RemoveAt(idx);

            // register delete action for undo/redo
            History.RegisterUndo(new DeleteAction(this, item));

            // clear selection
            Selected = null;
            UpdateVisualSelectionStyles();
            ShowGripsFor(null);
        }

        public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(
            nameof(Image), typeof(ImageSource), typeof(LabelEditor), new PropertyMetadata(null, OnImageChanged));

        public ImageSource? Image
        {
            get => (ImageSource?)GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
        }

        /// <summary>
        /// Default label text used when creating new LabelRect entries.
        /// Implemented as a DependencyProperty so it can be data-bound and styled.
        /// </summary>
        public static readonly DependencyProperty DefaultLabelProperty = DependencyProperty.Register(
            nameof(DefaultLabel), typeof(string), typeof(LabelEditor), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDefaultLabelChanged));

        /// <summary>
        /// The default label text. Can be bound from view-models or set in XAML.
        /// </summary>
        public string DefaultLabel
        {
            get => (string)GetValue(DefaultLabelProperty);
            set => SetValue(DefaultLabelProperty, value);
        }

        private static void OnDefaultLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // No default runtime action required - property exists for binding and usage by consumers.
            // If needed in the future, update behavior when DefaultLabel changes (for example, update UI hints).
        }

        public LabelEditor()
        {
            Focusable = true;
            Background = Brushes.Transparent;

            // Ensure that content (background image and visuals) is clipped to the control bounds
            // so that when the control is placed inside a smaller parent (for example a Border)
            // the image and drawn rectangles do not overlap the parent's client area.
            ClipToBounds = true;

            _backgroundImage = new Image { Stretch = Stretch.None };
            Children.Add(_backgroundImage);

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            KeyDown += OnKeyDown;
            Rectangles.CollectionChanged += Rectangles_CollectionChanged;
        }

        /// <summary>
        /// Raised when the position, size or label of a LabelRect is changed.
        /// The event argument is the new LabelRect value.
        /// </summary>
        public event EventHandler<LabelRect?>? LabelChanged;

        private void Rectangles_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Rebuild visuals to reflect current collection state
            SynchronizeVisuals();

            try
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace &&
                    e.OldItems != null && e.NewItems != null)
                {
                    int count = Math.Min(e.OldItems.Count, e.NewItems.Count);
                    for (int i = 0; i < count; i++)
                    {
                        if (e.OldItems[i] is LabelRect oldRect && e.NewItems[i] is LabelRect newRect)
                        {
                            if (oldRect.Id == newRect.Id)
                            {
                                // compare Rect and Label
                                if (!oldRect.Rect.Equals(newRect.Rect) || !string.Equals(oldRect.Label, newRect.Label, StringComparison.Ordinal))
                                {
                                    LabelChanged?.Invoke(this, newRect);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private bool HasImage => _backgroundImage?.Source != null;

        public ActionsHistory History { get => _history; set => _history = value; }

        private Rect GetImageBounds()
        {
            if (!HasImage) return Rect.Empty;
            double left = GetLeft(_backgroundImage);
            double top = GetTop(_backgroundImage);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            double width = _backgroundImage.Source.Width;
            double height = _backgroundImage.Source.Height;
            return new Rect(left, top, width, height);
        }

        private Rect ClampRectMovementToImage(Rect rect)
        {
            if (!HasImage) return rect;
            var img = GetImageBounds();
            // Start with incoming values
            double x = rect.X;
            double y = rect.Y;
            double r = rect.Right;
            double b = rect.Bottom;

            // If left is outside image, snap to left edge
            if (x < img.X)
            {
                x = img.X;
            }

            // If top is outside image, snap to top edge
            if (y < img.Y)
            {
                y = img.Y;
            }

            // If right is outside image, snap to right edge
            if (r > img.Right)
            {
                x -= r - img.Right;
            }

            // If bottom is outside image, snap to bottom edge
            if (b > img.Bottom)
            {
                y -= b - img.Bottom;
            }

            return new Rect(x, y, rect.Width, rect.Height);
        }

        private Rect ClampRectToImage(Rect rect)
        {
            if (!HasImage) return rect;
            var img = GetImageBounds();
            // Start with incoming values
            double x = rect.X;
            double y = rect.Y;
            double w = rect.Width;
            double h = rect.Height;

            // If left is outside image, snap to left edge
            if (x < img.X)
            {
                w -= (img.X - x);
                x = img.X;
            }

            // If top is outside image, snap to top edge
            if (y < img.Y)
            {
                h -= (img.Y - y);
                y = img.Y;
            }

            // Compute available space to the right and bottom from current position
            double maxW = Math.Max(0, img.Right - x);
            double maxH = Math.Max(0, img.Bottom - y);

            // If pointer extends beyond the right edge, keep left/top and reduce width to fit
            if (w > maxW)
            {
                w = maxW;
            }

            // If pointer extends beyond the bottom edge, keep left/top and reduce height to fit
            if (h > maxH)
            {
                h = maxH;
            }

            return new Rect(x, y, Math.Max(0, w), Math.Max(0, h));
        }

        private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LabelEditor le)
            {
                le._backgroundImage.Source = (ImageSource?)e.NewValue;
                le.UpdateBackgroundLayout();
            }
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            UpdateBackgroundLayout();
            return base.ArrangeOverride(arrangeSize);
        }

        private void UpdateBackgroundLayout()
        {
            if (_backgroundImage.Source == null)
                return;

            // Align the image to the top-left corner of the client area
            var img = _backgroundImage;
            if (img.Source.Width > 0 && img.Source.Height > 0)
            {
                // X is aligned to the left
                double x = 0;
                // Y is aligned to the top
                double y = 0;
                SetLeft(img, x);
                SetTop(img, y);
            }
            else
            {
                SetLeft(img, 0);
                SetTop(img, 0);
            }
        }

        private void OnMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            Focus();

            if (this.Image == null)
            {
                return;
            }

            CaptureMouse();
            _mouseStart = e.GetPosition(this);

            // Hit test existing rectangles (from topmost to bottom)
            var hit = HitTestVisual(_mouseStart);
            if (hit != null)
            {
                _activeVisual = hit;
                var rect = GetRectFromVisual(_activeVisual);

                // capture operation target id and original rect for undo/redo
                _operationTargetId = GetIdFromVisual(_activeVisual) ?? Guid.Empty;
                _operationOriginalRect = rect;
                // select the clicked rectangle
                var id = _operationTargetId;
                if (id != Guid.Empty)
                {
                    Selected = Rectangles.FirstOrDefault(r => r.Id == id);
                    UpdateVisualSelectionStyles();
                    ShowGripsFor(_activeVisual);
                }

                // Check corners for resize
                var corner = GetCornerIfNear(_mouseStart, rect);
                if (corner != Corner.None)
                {
                    _isResizing = true;
                    _resizeCorner = corner;
                }
                else
                {
                    // start move
                    _isMoving = true;
                    _moveStartOffset = new Point(_mouseStart.X - rect.X, _mouseStart.Y - rect.Y);
                }
                // show grips for selected rectangle
                ShowGripsFor(_activeVisual);
            }
            else
            {
                if (CanDraw())
                {
                    // start drawing a new rectangle (mouse down)
                    _isDrawing = true;
                    var r = CreateVisualRect(new Rect(_mouseStart, new Size(0, 0)));
                    _activeVisual = r;
                    _visuals.Add(r);
                    Children.Add(r);
                    // also add grips to visual tree (positions will be updated on move)
                    if (_grips.TryGetValue(r, out var grips))
                    {
                        foreach (var g in grips) Children.Add(g);
                    }
                    // new item will be created on mouse up; mark active visual but selection set when added
                }
            }

            e.Handled = true;
        }

        private bool CanDraw()
        {
            return this.Image != null && !string.IsNullOrEmpty(this.DefaultLabel);
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);

            // When not interacting, detect hover near corners and show grips
            if (!_isDrawing && !_isMoving && !_isResizing)
            {
                Rectangle? cornerVisual = null;
                for (int i = _visuals.Count - 1; i >= 0; i--)
                {
                    var v = _visuals[i];
                    var r = GetRectFromVisual(v);
                    if (GetCornerIfNear(pos, r) != Corner.None)
                    {
                        cornerVisual = v;
                        break;
                    }
                }

                if (cornerVisual != _hoveredVisual)
                {
                    _hoveredVisual = cornerVisual;
                    // Prefer showing grips for hovered visual; fall back to active selection
                    ShowGripsFor(_hoveredVisual ?? _activeVisual);
                }
            }

            // If no active visual (not drawing/moving/resizing), nothing else to do
            if (_activeVisual == null)
            {
                e.Handled = true;
                return;
            }

            if (_isDrawing)
            {
                var rect = NormalizeRect(new Rect(_mouseStart, pos));
                rect = ClampRectToImage(rect);
                UpdateVisualRect(_activeVisual, rect);
            }
            else if (_isMoving)
            {
                var newX = pos.X - _moveStartOffset.X;
                var newY = pos.Y - _moveStartOffset.Y;
                var rect = GetRectFromVisual(_activeVisual);
                var newRect = new Rect(newX, newY, rect.Width, rect.Height);
                newRect = ClampRectMovementToImage(newRect);
                UpdateVisualRect(_activeVisual, newRect);
            }
            else if (_isResizing)
            {
                var rect = GetRectFromVisual(_activeVisual);
                var newRect = rect;
                switch (_resizeCorner)
                {
                    case Corner.TopLeft:
                        (newRect, _resizeCorner) = CreateRectForResizing(pos, new Point(rect.Right, rect.Bottom), _resizeCorner);
                        break;
                    case Corner.TopRight:
                        (newRect, _resizeCorner) = CreateRectForResizing(new Point(rect.Left, pos.Y), new Point(pos.X, rect.Bottom), _resizeCorner);
                        break;
                    case Corner.BottomLeft:
                        (newRect, _resizeCorner) = CreateRectForResizing(new Point(pos.X, rect.Top), new Point(rect.Right, pos.Y), _resizeCorner);
                        break;
                    case Corner.BottomRight:
                        (newRect, _resizeCorner) = CreateRectForResizing(rect.TopLeft, pos, _resizeCorner);
                        break;
                }

                newRect = ClampRectToImage(newRect);
                UpdateVisualRect(_activeVisual, newRect);
            }

            e.Handled = true;
        }

        private void OnMouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
        {
            if (_activeVisual == null)
            {
                ReleaseMouseCapture();
                return;
            }

            var finalRect = GetRectFromVisual(_activeVisual);

            if (_isDrawing)
            {
                // add to collection if size > 0
                if (finalRect.Width > 0 && finalRect.Height > 0)
                {
                    var clamped = ClampRectToImage(finalRect);
                    var item = new LabelRect(Guid.NewGuid(), clamped, this.DefaultLabel);
                    Rectangles.Add(item);
                    History.RegisterUndo(new AddAction(this, item));
                    // select the newly created item
                    Selected = item;
                    UpdateVisualSelectionStyles();
                }
                else
                {
                    // remove zero-size visual
                    Children.Remove(_activeVisual);
                    _visuals.Remove(_activeVisual);
                }
            }
            else if (_isMoving || _isResizing)
            {
                // find model by captured operation id
                if (_operationTargetId != Guid.Empty)
                {
                    int idx = -1;
                    for (int i = 0; i < Rectangles.Count; i++)
                    {
                        if (Rectangles[i].Id == _operationTargetId) { idx = i; break; }
                    }

                    if (idx >= 0)
                    {
                        var before = _operationOriginalRect;
                        var after = finalRect;
                        if (before != after)
                        {
                            var old = Rectangles[idx];
                            // If after has zero width or height, delete the rectangle
                            if (after.Width <= 0 || after.Height <= 0)
                            {
                                // register delete action
                                History.RegisterUndo(new DeleteAction(this, old));
                                // remove from collection
                                Rectangles.RemoveAt(idx);
                                // clear selection
                                if (Selected != null && Selected.Id == old.Id)
                                {
                                    Selected = null;
                                }
                                UpdateVisualSelectionStyles();
                                ShowGripsFor(null);
                            }
                            else
                            {
                                var newRect = new LabelRect(old.Id, after, old.Label);
                                Rectangles[idx] = newRect;
                                History.RegisterUndo(new MoveResizeAction(this, old.Id, before, after));
                                // notify listeners about label/rect change
                                LabelChanged?.Invoke(this, newRect);
                                // set selection to the moved/resized item
                                Selected = Rectangles[idx];
                                UpdateVisualSelectionStyles();
                            }
                        }
                    }
                }
            }

            _isDrawing = _isMoving = _isResizing = false;
            _activeVisual = null;
            _operationTargetId = Guid.Empty;
            // hide grips after finishing interaction
            ShowGripsFor(null);
            ReleaseMouseCapture();
            e.Handled = true;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                Undo();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.Y || (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control | ModifierKeys.Shift))))
            {
                Redo();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
        }

        private Rectangle CreateVisualRect(Rect rect)
        {
            var r = new Rectangle
            {
                Stroke = Brushes.Green,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(64, 0, 255, 0))
            };

            UpdateVisualRect(r, rect);

            // create corner grips (visual cues for resizing)
            var gripBrush = new SolidColorBrush(Color.FromArgb(100, 0, 100, 0)); // dark green with ~70% opacity
            var tl = new Rectangle
            {
                Width = 8,
                Height = 8,
                Fill = gripBrush,
                Stroke = gripBrush,
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false // grips are visual cues; resizing uses proximity logic
            };
            var tr = new Rectangle
            {
                Width = 8,
                Height = 8,
                Fill = gripBrush,
                Stroke = gripBrush,
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            var bl = new Rectangle
            {
                Width = 8,
                Height = 8,
                Fill = gripBrush,
                Stroke = gripBrush,
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            var br = new Rectangle
            {
                Width = 8,
                Height = 8,
                Fill = gripBrush,
                Stroke = gripBrush,
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };

            _grips[r] = new[] { tl, tr, bl, br };

            // tag may be set later when model is available
            r.Tag = null;
            return r;
        }

        private void UpdateVisualRect(Rectangle visual, Rect rect)
        {
            SetLeft(visual, rect.X);
            SetTop(visual, rect.Y);
            visual.Width = rect.Width;
            visual.Height = rect.Height;
            // update grips positions if present
            if (_grips.TryGetValue(visual, out var grips))
            {
                // order: TL, TR, BL, BR
                UpdateGripPosition(grips[0], rect.TopLeft);
                UpdateGripPosition(grips[1], rect.TopRight);
                UpdateGripPosition(grips[2], rect.BottomLeft);
                UpdateGripPosition(grips[3], rect.BottomRight);
            }
        }

        private void UpdateGripPosition(Rectangle grip, Point center)
        {
            if (grip == null) return;
            // center the grip on the corner
            double left = center.X - grip.Width / 2.0;
            double top = center.Y - grip.Height / 2.0;
            if (!Children.Contains(grip))
            {
                Children.Add(grip);
                Canvas.SetZIndex(grip, 1000);
            }
            SetLeft(grip, left);
            SetTop(grip, top);
        }

        private Rect GetRectFromVisual(Rectangle visual)
        {
            var x = GetLeft(visual);
            var y = GetTop(visual);
            if (double.IsNaN(x)) x = 0;
            if (double.IsNaN(y)) y = 0;
            return new Rect(x, y, visual.Width, visual.Height);
        }

        private Rectangle? HitTestVisual(Point p)
        {
            for (int i = _visuals.Count - 1; i >= 0; i--)
            {
                var v = _visuals[i];
                var r = GetRectFromVisual(v);
                if (r.Contains(p))
                    return v;
            }
            return null;
        }

        private Guid? GetIdFromVisual(Rectangle visual)
        {
            if (visual?.Tag is Guid g)
                return g;
            return null;
        }

        private void ShowGripsFor(Rectangle? visual)
        {
            // hide all grips first
            foreach (var kv in _grips)
            {
                foreach (var g in kv.Value)
                {
                    g.Visibility = Visibility.Collapsed;
                }
            }

            if (visual == null) return;

            // Always show grips for the Selected model if present
            if (Selected != null)
            {
                // find visual for selected
                var selVisual = _visuals.FirstOrDefault(v => (v.Tag is Guid id) && id == Selected.Id);
                if (selVisual != null && _grips.TryGetValue(selVisual, out var selGrips))
                {
                    foreach (var g in selGrips) g.Visibility = Visibility.Visible;
                }
                // if requested visual is not selected, also show its grips
                if (visual != selVisual && _grips.TryGetValue(visual, out var otherGrips))
                {
                    foreach (var g in otherGrips) g.Visibility = Visibility.Visible;
                }
                return;
            }

            if (_grips.TryGetValue(visual, out var grips))
            {
                foreach (var g in grips)
                {
                    g.Visibility = Visibility.Visible;
                }
            }
        }

        private void UpdateVisualSelectionStyles()
        {
            for (int i = 0; i < _visuals.Count; i++)
            {
                var v = _visuals[i];
                var isSelected = false;
                if (v.Tag is Guid gid && Selected != null)
                    isSelected = gid == Selected.Id;

                if (isSelected)
                {
                    v.Stroke = Brushes.LimeGreen;
                    v.Fill = new SolidColorBrush(Color.FromArgb(64, 0, 255, 0));
                }
                else
                {
                    // darker green for non-selected
                    v.Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 0));
                    v.Fill = new SolidColorBrush(Color.FromArgb(64, 0, 150, 0));
                }
            }
            // Also ensure grips are updated (selected grips visible handled in ShowGripsFor)
        }

        private Corner GetCornerIfNear(Point p, Rect rect)
        {
            if (Distance(p, rect.TopLeft) <= CornerHitRadius) return Corner.TopLeft;
            if (Distance(p, rect.TopRight) <= CornerHitRadius) return Corner.TopRight;
            if (Distance(p, rect.BottomLeft) <= CornerHitRadius) return Corner.BottomLeft;
            if (Distance(p, rect.BottomRight) <= CornerHitRadius) return Corner.BottomRight;
            return Corner.None;
        }

        private static double Distance(Point a, Point b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static Rect NormalizeRect(Rect r)
        {
            if (r.Width < 0)
            {
                r = new Rect(r.X + r.Width, r.Y, -r.Width, r.Height);
            }
            if (r.Height < 0)
            {
                r = new Rect(r.X, r.Y + r.Height, r.Width, -r.Height);
            }
            return r;
        }

        private static (Rect, Corner) CreateRectForResizing(
            Point a,
            Point b,
            Corner c)
        {
            var x1 = a.X;
            var x2 = b.X;
            if (x2 < x1)
            {
                var t = x1;
                x1 = x2;
                x2 = t;

                if(c == Corner.TopLeft) c = Corner.TopRight;
                else if (c == Corner.TopRight) c = Corner.TopLeft;
                else if (c == Corner.BottomLeft) c = Corner.BottomRight;
                else if (c == Corner.BottomRight) c = Corner.BottomLeft;
            }

            var y1 = a.Y;
            var y2 = b.Y;

            if (y2 < y1)
            {
                var t = y1;
                y1 = y2;
                y2 = t;

                if(c == Corner.TopLeft) c = Corner.BottomLeft;
                else if (c == Corner.TopRight) c = Corner.BottomRight;
                else if (c == Corner.BottomLeft) c = Corner.TopLeft;
                else if (c == Corner.BottomRight) c = Corner.TopRight;
            }

            return (new Rect(new Point(x1, y1), new Point(x2, y2)), c);
        }

        private void SynchronizeVisuals()
        {
            // Remove all visuals except background
            foreach (var v in _visuals)
            {
                if (Children.Contains(v))
                    Children.Remove(v);
                if (_grips.TryGetValue(v, out var gs))
                {
                    foreach (var g in gs)
                        if (Children.Contains(g)) Children.Remove(g);
                    _grips.Remove(v);
                }
            }
            _visuals.Clear();

            // Recreate visuals from Rectangles
            foreach (var item in Rectangles)
            {
                var v = CreateVisualRect(item.Rect);
                v.Tag = item.Id;
                _visuals.Add(v);
                Children.Add(v);
                if (_grips.TryGetValue(v, out var gs))
                {
                    foreach (var g in gs) Children.Add(g);
                    // ensure grips positioned correctly
                    UpdateVisualRect(v, item.Rect);
                }
            }
        }

        // Public Undo/Redo wrappers
        public bool CanUndo() => History.CanUndo;
        public bool CanRedo() => History.CanRedo;
        public void Undo() => History.Undo();
        public void Redo() => History.Redo();

        #region Actions

        private interface IAction : IHistoryAction
        {
            // Retained for compatibility with ActionsHistory
            void Undo();
            void Redo();
        }

        private class AddAction : IHistoryAction
        {
            private readonly LabelEditor _owner;
            private readonly LabelRect _item;

            public AddAction(LabelEditor owner, LabelRect item)
            {
                _owner = owner;
                _item = item;
            }

            public void Undo()
            {
                var idx = -1;
                for (int i = 0; i < _owner.Rectangles.Count; i++)
                {
                    if (_owner.Rectangles[i].Id == _item.Id) { idx = i; break; }
                }
                if (idx >= 0) _owner.Rectangles.RemoveAt(idx);
                // clear selection if the removed item was selected
                if (_owner.Selected != null && _owner.Selected.Id == _item.Id)
                {
                    _owner.Selected = null;
                    _owner.UpdateVisualSelectionStyles();
                }
            }

            public void Redo()
            {
                _owner.Rectangles.Add(new LabelRect(_item.Id, _item.Rect, _item.Label));
                // select recreated item
                _owner.Selected = (_owner.Rectangles.LastOrDefault(r => r.Id == _item.Id));
                _owner.UpdateVisualSelectionStyles();
            }
        }

        private class MoveResizeAction : IHistoryAction
        {
            private readonly LabelEditor _owner;
            private readonly Guid _id;
            private readonly Rect _before;
            private readonly Rect _after;

            public MoveResizeAction(LabelEditor owner, Guid id, Rect before, Rect after)
            {
                _owner = owner;
                _id = id;
                _before = before;
                _after = after;
            }

            public void Undo()
            {
                for (int i = 0; i < _owner.Rectangles.Count; i++)
                {
                    if (_owner.Rectangles[i].Id == _id)
                    {
                        var old = _owner.Rectangles[i];
                        _owner.Rectangles[i] = new LabelRect(old.Id, _before, old.Label);
                        // select this item after undo
                        _owner.Selected = _owner.Rectangles[i];
                        _owner.UpdateVisualSelectionStyles();
                        break;
                    }
                }
            }

            public void Redo()
            {
                for (int i = 0; i < _owner.Rectangles.Count; i++)
                {
                    if (_owner.Rectangles[i].Id == _id)
                    {
                        var old = _owner.Rectangles[i];
                        _owner.Rectangles[i] = new LabelRect(old.Id, _after, old.Label);
                        // select this item after redo
                        _owner.Selected = _owner.Rectangles[i];
                        _owner.UpdateVisualSelectionStyles();
                        break;
                    }
                }
            }

        }

        private class DeleteAction : IHistoryAction
        {
            private readonly LabelEditor _owner;
            private readonly LabelRect _item;

            public DeleteAction(LabelEditor owner, LabelRect item)
            {
                _owner = owner;
                _item = item;
            }

            public void Undo()
            {
                // restore the item
                _owner.Rectangles.Add(new LabelRect(_item.Id, _item.Rect, _item.Label));
                _owner.Selected = _owner.Rectangles.FirstOrDefault(r => r.Id == _item.Id);
                _owner.UpdateVisualSelectionStyles();
            }

            public void Redo()
            {
                var idx = -1;
                for (int i = 0; i < _owner.Rectangles.Count; i++)
                {
                    if (_owner.Rectangles[i].Id == _item.Id) { idx = i; break; }
                }
                if (idx >= 0) _owner.Rectangles.RemoveAt(idx);
                if (_owner.Selected != null && _owner.Selected.Id == _item.Id)
                {
                    _owner.Selected = null;
                    _owner.UpdateVisualSelectionStyles();
                }
            }
        }

        #endregion

        private enum Corner
        {
            None,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }
    }
}
