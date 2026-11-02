using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;

namespace Detective.Views.Controls
{
    /// <summary>
    /// Interaction logic for LabelList.xaml
    /// </summary>
    public partial class LabelList : UserControl
    {
        // Labels collection
        public ObservableCollection<string> Labels { get; } = new ObservableCollection<string>();

        public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
            nameof(SelectedIndex), typeof(int), typeof(LabelList), new PropertyMetadata(-1, OnSelectedIndexChanged));

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LabelList ll)
            {
                ll.UpdateButtons();
                try
                {
                    int oldIndex = (int)e.OldValue;
                    int newIndex = (int)e.NewValue;
                    if (oldIndex != newIndex)
                    {
                        ll.SelectedChanged?.Invoke(ll, newIndex);
                    }
                }
                catch { }
            }
        }

        public LabelList()
        {
            InitializeComponent();
            Labels.CollectionChanged += Labels_CollectionChanged;
            UpdateButtons();
            AddButton.Click += AddButton_Click;
            SearchTextBox.KeyDown += SearchTextBox_KeyDown;
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;
        }

        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter
                && AddButton.IsEnabled)
            { 
                AddNewLabel();
            }
        }

        public event EventHandler<int>? SelectedChanged;

        private void AddButton_Click(object? sender, RoutedEventArgs e)
        {
            AddNewLabel();
        }

        private void AddNewLabel()
        {
            var text = SearchTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // avoid duplicates (case-insensitive)
            if (Labels.Any(l => string.Equals(l, text, StringComparison.OrdinalIgnoreCase)))
            {
                SearchTextBox.Text = string.Empty;
                return;
            }

            Labels.Add(text);
            // select new
            SelectedIndex = Labels.Count - 1;
            UpdateButtons();

            SearchTextBox.Text = string.Empty;
            AddButton.IsEnabled = false;
        }

        private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            var text = SearchTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                AddButton.IsEnabled = false;
                return;
            }

            // find first match (case-insensitive contains)
            int idx = -1;
            for (int i = 0; i < Labels.Count; i++)
            {
                if (Labels[i].IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    idx = i; break;
                }
            }

            if (idx >= 0)
            {
                SelectedIndex = idx;
                AddButton.IsEnabled = !Labels[idx].Equals(text, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                SelectedIndex = -1;
                // enable add only when there is text and no match
                AddButton.IsEnabled = true;
            }
        }

        private void Labels_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // adjust SelectedIndex in case items removed
            if (e.Action == NotifyCollectionChangedAction.Remove && SelectedIndex >= Labels.Count)
            {
                SelectedIndex = -1;
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            LabelsContainer.Children.Clear();

            // ensure SelectedIndex is in range
            if (SelectedIndex < -1) SelectedIndex = -1;
            if (SelectedIndex >= Labels.Count) SelectedIndex = -1;

            for (int i = 0; i < Labels.Count; i++)
            {
                var text = Labels[i];
                var btn = new Wpf.Ui.Controls.Button
                {
                    Content = text,
                    Margin = new Thickness(5,5,5,5)
                };

                // set appearance (Success for selected, Primary otherwise)
                SetButtonAppearance(btn, i == SelectedIndex ? "Success" : "Secondary");

                int index = i; // capture
                btn.Click += (s, e) => { SelectedIndex = index; };

                // add context menu with Delete item (uses Fluent System Icon)
                try
                {
                    var menu = new ContextMenu();
                    var mi = new System.Windows.Controls.MenuItem { Header = "Delete" };

                    // create SymbolIcon and set its Symbol to Delete12 via reflection
                    var symbolIcon = Activator.CreateInstance(typeof(Wpf.Ui.Controls.SymbolIcon));
                    var prop = symbolIcon!.GetType().GetProperty("Symbol");
                    if (prop != null)
                    {
                        var enumType = prop.PropertyType;
                        try
                        {
                            var enumVal = Enum.Parse(enumType, "Delete12");
                            prop.SetValue(symbolIcon, enumVal);
                        }
                        catch { }
                    }

                    // assign Icon if available
                    mi.Icon = symbolIcon as UIElement;

                    mi.Click += (s, e) =>
                    {
                        // if this was selected, clear selection
                        if (SelectedIndex == index)
                            SelectedIndex = -1;
                        if (index >= 0 && index < Labels.Count)
                            Labels.RemoveAt(index);
                    };

                    menu.Items.Add(mi);
                    btn.ContextMenu = menu;
                }
                catch { }

                LabelsContainer.Children.Add(btn);
            }
        }

        private void SetButtonAppearance(Wpf.Ui.Controls.Button btn, string appearanceName)
        {
            // find static AppearanceProperty on the Button type
            var dpField = typeof(Wpf.Ui.Controls.Button).GetField("AppearanceProperty", BindingFlags.Public | BindingFlags.Static);
            if (dpField == null) return;
            var dp = dpField.GetValue(null) as DependencyProperty;
            if (dp == null) return;
            var enumType = dp.PropertyType;
            try
            {
                var enumVal = Enum.Parse(enumType, appearanceName);
                btn.SetValue(dp, enumVal);
            }
            catch
            {
                // ignore parse errors
            }
        }
    }
}
