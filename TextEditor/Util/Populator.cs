using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace TextEditor.Util
{
    public static class Populator
    {
        public static void PopulateSystemFonts(ComboBox fontComboBox)
        {
            // Получаем список системных шрифтов и добавляем их в FontComboBox
            foreach (FontFamily fontFamily in Fonts.SystemFontFamilies)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = fontFamily.Source;
                fontComboBox.Items.Add(item);
            }
            fontComboBox.SelectedIndex = 2;
        }


        public static void PopulateFontSizes(ComboBox fontSizeComboBox)
        {
            // Заполняем ComboBox системными размерами шрифта
            for (int i = 6; i <= 48; i += 2)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = i;
                fontSizeComboBox.Items.Add(item);
            }
            fontSizeComboBox.SelectedIndex = 6;
        }


        public static void PopulateColors(ComboBox colorComboBox)
        {
            colorComboBox.Items.Add(Colors.Black);
            colorComboBox.Items.Add(Colors.Red);
            colorComboBox.Items.Add(Colors.Blue);
            colorComboBox.Items.Add(Colors.Green);
            colorComboBox.Items.Add(Colors.Orange);
            colorComboBox.Items.Add(Colors.Yellow);
            colorComboBox.Items.Add(Colors.DarkBlue);
            colorComboBox.Items.Add(Colors.Violet);
            colorComboBox.Items.Add(Colors.White);
            colorComboBox.SelectedIndex = 0;
        }


        public static void PopulateBackgroundColors(ComboBox bgColorComboBox)
        {
            bgColorComboBox.Items.Add(Colors.White);
            bgColorComboBox.Items.Add(Colors.Black);
            bgColorComboBox.Items.Add(Colors.Red);
            bgColorComboBox.Items.Add(Colors.Blue);
            bgColorComboBox.Items.Add(Colors.Green);
            bgColorComboBox.Items.Add(Colors.Orange);
            bgColorComboBox.Items.Add(Colors.Yellow);
            bgColorComboBox.Items.Add(Colors.DarkBlue);
            bgColorComboBox.Items.Add(Colors.Violet);
            bgColorComboBox.SelectedIndex = 0;
        }
    }
}
