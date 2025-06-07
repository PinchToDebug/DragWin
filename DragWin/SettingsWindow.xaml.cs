using Microsoft.Win32;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = Wpf.Ui.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace DragWin
{
    public partial class SettingsWindow : FluentWindow
    {
        public SettingsWindow()
        {
            InitializeComponent();
            this.Loaded += LoadBlockedListFromRegistry;
        }
        private void WriteAllRegistry(string keyName, object value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\DragWin"))
                {
                    key.SetValue(keyName, value);
                }
            }
            catch { }
        }
        private void SaveBlockedListToRegistry()
        {
            var sb = new StringBuilder();
            foreach (var child in blockedList.Children)
            {
                if (child is StackPanel panel)
                {
                    var texts = new List<string>();
                    foreach (var element in panel.Children)
                    {
                        if (element is TextBlock tb)
                        {
                            texts.Add(tb.Text);
                        }
                    }
                    if (texts.Count >= 3)
                    {
                        sb.Append(string.Join("<|>", texts));
                        sb.Append("|,|");
                    }
                }
            }
            if (sb.Length > 0) sb.Length -= 3;
            WriteAllRegistry("BlockedItems", sb.ToString());
        }
        private void LoadBlockedListFromRegistry(object sender, RoutedEventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\DragWin"))
                {
                    if (key == null) return;

                    var blockedItems = key.GetValue("BlockedItems") as string;
                    if (string.IsNullOrEmpty(blockedItems)) return;

                    blockedList.Children.Clear();

                    var panels = blockedItems.Split(new[] { "|,|" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var panelData in panels)
                    {
                        var texts = panelData.Split(new[] { "<|>" }, StringSplitOptions.None);
                        if (texts.Length < 3) continue;

                        var panel = CreateBlockedItemPanel(texts[0], texts[1], texts[2]);
                        blockedList.Children.Add(panel);
                    }
                }
            }
            catch
            {
            }
        }

        private StackPanel CreateBlockedItemPanel(string text1, string text2, string text3)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };

            var delete_btn = new Button
            {
                Icon = new SymbolIcon { Symbol = SymbolRegular.Delete24 },
                Padding = new Thickness(3),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#FFED6262")!,
                Height = 20,
                FontSize = 14
            };
            var edit_btn = new Button
            {
                Icon = new SymbolIcon { Symbol = SymbolRegular.Edit20 },
                Padding = new Thickness(3),
                Margin = new Thickness(4, 0, 8, 0),
                Height = 20,
                FontSize = 14
            };
            var cancel_btn = new Button
            {
                Icon = new SymbolIcon { Symbol = SymbolRegular.Dismiss24 },
                Padding = new Thickness(3),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#FFED6262")!,
                Height = 20,
                FontSize = 14,
                Visibility = Visibility.Collapsed
            };

            var tb1 = new TextBlock
            {
                Text = text1,
                Margin = new Thickness(2),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#FFB0A0FF")!
            };
            var tb2 = new TextBlock
            {
                Text = text2,
                Margin = new Thickness(2),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#FF90FFC0")!
            };
            var tb3 = new TextBlock
            {
                Text = text3,
                Margin = new Thickness(2),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#FFFFD580")!
            };

            var tbox1 = new TextBox
            {
                Text = tb1.Text,
                Margin = new Thickness(2),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#FFB0A0FF")!,
                Visibility = Visibility.Collapsed
            };
            var tbox2 = new TextBox
            {
                Text = tb2.Text,
                Margin = new Thickness(2),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#FF90FFC0")!,
                Visibility = Visibility.Collapsed
            };
            var tbox3 = new TextBox
            {
                Text = tb3.Text,
                Margin = new Thickness(2),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#FFFFD580")!,
                Visibility = Visibility.Collapsed
            };

            delete_btn.Click += (s, e) =>
            {
                blockedList.Children.Remove(panel);
                SaveBlockedListToRegistry();
            };

            edit_btn.Click += (s, e) =>
            {
                if (edit_btn.Icon is SymbolIcon icon && icon.Symbol == SymbolRegular.Edit20)
                {
                    edit_btn.Foreground = (Brush)new BrushConverter().ConvertFromString("#FF66ED62")!;
                    tb1.Visibility = Visibility.Collapsed;
                    tb2.Visibility = Visibility.Collapsed;
                    tb3.Visibility = Visibility.Collapsed;
                    tbox1.Text = tb1.Text;
                    tbox2.Text = tb2.Text;
                    tbox3.Text = tb3.Text;
                    tbox1.Visibility = Visibility.Visible;
                    tbox2.Visibility = Visibility.Visible;
                    tbox3.Visibility = Visibility.Visible;
                    delete_btn.Visibility = Visibility.Collapsed;
                    cancel_btn.Visibility = Visibility.Visible;
                    edit_btn.Icon = new SymbolIcon { Symbol = SymbolRegular.Checkmark24 };
                }
                else
                {
                    edit_btn.Foreground = Brushes.White;
                    edit_btn.Icon = new SymbolIcon { Symbol = SymbolRegular.Edit20 };
                    tb1.Visibility = Visibility.Visible;
                    tb2.Visibility = Visibility.Visible;
                    tb3.Visibility = Visibility.Visible;
                    tb1.Text = tbox1.Text;
                    tb2.Text = tbox2.Text;
                    tb3.Text = tbox3.Text;
                    tbox1.Visibility = Visibility.Collapsed;
                    tbox2.Visibility = Visibility.Collapsed;
                    tbox3.Visibility = Visibility.Collapsed;
                    delete_btn.Visibility = Visibility.Visible;
                    cancel_btn.Visibility = Visibility.Collapsed;
                    SaveBlockedListToRegistry();
                }
            };

            cancel_btn.Click += (s, e) =>
            {
                tbox1.Text = tb1.Text;
                tbox2.Text = tb2.Text;
                tbox3.Text = tb3.Text;
                edit_btn.Foreground = Brushes.White;
                edit_btn.Icon = new SymbolIcon { Symbol = SymbolRegular.Edit20 };
                tb1.Visibility = Visibility.Visible;
                tb2.Visibility = Visibility.Visible;
                tb3.Visibility = Visibility.Visible;
                tbox1.Visibility = Visibility.Collapsed;
                tbox2.Visibility = Visibility.Collapsed;
                tbox3.Visibility = Visibility.Collapsed;
                delete_btn.Visibility = Visibility.Visible;
                cancel_btn.Visibility = Visibility.Collapsed;
            };

            panel.MouseEnter += (s, e) => panel.Background = (Brush)new BrushConverter().ConvertFromString("#0CFFFFFF")!;
            panel.MouseLeave += (s, e) => panel.Background = null;

            panel.Children.Add(delete_btn);
            panel.Children.Add(cancel_btn);
            panel.Children.Add(edit_btn);
            panel.Children.Add(tb1);
            panel.Children.Add(tb2);
            panel.Children.Add(tb3);
            panel.Children.Add(tbox1);
            panel.Children.Add(tbox2);
            panel.Children.Add(tbox3);

            return panel;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var panel = CreateBlockedItemPanel(
                tempblacklisted_TB1.Text,
                tempblacklisted_TB2.Text,
                tempblacklisted_TB3.Text);

            blockedList.Children.Add(panel);
            SaveBlockedListToRegistry();
        }
    }
}
