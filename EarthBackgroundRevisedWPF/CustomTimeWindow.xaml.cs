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
        public CustomTimeWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void HoursTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            int currentVal = 0;
            if(HoursTextBox.Text.Length > 0)
            {
                currentVal = Convert.ToInt32(HoursTextBox.Text);
            }

            if (!(KeyIsNumeric(e.Key) || e.Key == Key.Tab) || !(currentVal <= 2 && keyIsBetween(e.Key,0,4)))
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
    }
}
