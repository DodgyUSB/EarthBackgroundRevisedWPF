using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EarthBackgroundRevisedWPF
{
    /// <summary>
    /// Interaction logic for CustomTimeWindow.xaml
    /// </summary>
    public partial class CustomTimeWindow : Window
    {
        public int Ticks = 0;
        private Key[] validTextBoxControlKeys = new Key[] { Key.Back, Key.Delete, Key.Tab, Key.Left, Key.Right };

        public CustomTimeWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            int hours = 0;
            if(HoursTextBox.Text.Length > 0)
            {
                hours = Convert.ToInt32(HoursTextBox.Text);
            }
            int mins = 0;
            if(MinsTextBox.Text.Length > 0)
            {
                mins = Convert.ToInt32(MinsTextBox.Text);
            }
            int secs = 0;
            if(SecsTextBox.Text.Length > 0)
            {
                secs = Convert.ToInt32(SecsTextBox.Text);
            }
            Ticks = (hours * 1200) + (mins * 60) + secs;
            if (Ticks > 0)
            {
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
            this.Hide();
        }

        private void HoursTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Console.WriteLine(e.Key);
            int currentVal = 0;
            if(HoursTextBox.Text.Length > 0)
            {
                currentVal = Convert.ToInt32(HoursTextBox.Text);
            }
            //Console.WriteLine(validTextBoxControlKeys.Contains(e.Key));
            if (!((KeyIsNumeric(e.Key) && ((currentVal == 2 && keyIsBetween(e.Key,0,4)) || currentVal < 2)) || validTextBoxControlKeys.Contains(e.Key)))
            {
                e.Handled = true;
            }
        }

        private bool KeyIsNumeric(Key key)
        {
            int keyVal = (int)key;
            return (keyVal >= 34 && keyVal <= 43) || (keyVal >=74 && keyVal <= 83);
        }

        private bool keyIsBetween(Key key, int lowerBound, int upperBound)
        {
            int keyVal = (int)key;
            if (KeyIsNumeric(key))
            {
                return (keyVal >= (lowerBound + 74) && keyVal <= (upperBound + 74)) || (keyVal <= (upperBound + 34) && keyVal >= (lowerBound));
            }
            else
            {
                return false;
            }
        }

        private void MinsTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Console.WriteLine(e.Key);
            int currentVal = 0;
            if (MinsTextBox.Text.Length > 0)
            {
                currentVal = Convert.ToInt32(MinsTextBox.Text);
            }
            //Console.WriteLine(validTextBoxControlKeys.Contains(e.Key));
            if (!((KeyIsNumeric(e.Key) && currentVal < 6) || validTextBoxControlKeys.Contains(e.Key)))
            {
                e.Handled = true;
            }
        }

        private void SecsTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Console.WriteLine(e.Key);
            int currentVal = 0;
            if (SecsTextBox.Text.Length > 0)
            {
                currentVal = Convert.ToInt32(SecsTextBox.Text);
            }
            if (!((KeyIsNumeric(e.Key) && currentVal < 6) || validTextBoxControlKeys.Contains(e.Key)))
            {
                e.Handled = true;
            }
        }
    }
}
