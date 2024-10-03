using Microsoft.VisualBasic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextEditor.FileSystem;
using TextEditor.Util;
using System.Windows.Threading;
using FontFamily = System.Windows.Media.FontFamily;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;

namespace TextEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string currentFileName;
        private int currentPageIndex = 0;
        private double scale = 1;
        private FlowDocument flowDocument;
        private RichTextBox currentRichTextBox;
        public event PropertyChangedEventHandler PropertyChanged;
        Binding binding = new Binding("Scale");
        private TextPointer selectionStart;
        private TextPointer selectionEnd;
        private bool updatePanel=false;
        private bool isDirty = false;
        private List<TextRange> highlightedRanges = new List<TextRange>();
        int searchStatus = 0;
        private Stack<string> undoStack = new Stack<string>();
        private Stack<string> redoStack = new Stack<string>();

        private void MarkAsDirty()
        {
            isDirty = true;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public double Scale
        {
            get { return scale; }
            set
            {
                if (scale != value)
                {
                    scale = value;
                    OnPropertyChanged(nameof(Scale));
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeDocument();
            richTextBox.IsEnabled = false;
            richTextBox.Visibility = Visibility.Collapsed;
            Populator.PopulateSystemFonts(FontComboBox);
            Populator.PopulateFontSizes(FontSizeComboBox);
            Populator.PopulateColors(ColorComboBox);
            Populator.PopulateBackgroundColors(BackgroundColorComboBox);
            UpdateMainWindowTitle();
            richTextBox.TextChanged += RichTextBox_TextChanged;
            binding.Source = this;
            scaleSlider.SetBinding(Slider.ValueProperty, binding);
            scaleSlider.ValueChanged += ScaleSlider_ValueChanged;
            UpdateScaleValueLabel();
            richTextBox.PreviewMouseDown += RichTextBox_PreviewMouseDown;
            richTextBox.PreviewMouseMove += RichTextBox_PreviewMouseMove;
            richTextBox.PreviewMouseUp += RichTextBox_PreviewMouseUp;
            richTextBox.SelectionChanged += richTextBox_SelectionChanged;
            richTextBox.TextChanged += (s, e) => MarkAsDirty();
        }

        private void richTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (richTextBox.CaretPosition != null)
            {
                if (searchStatus==1)
                {
                    ClearSearch();
                    searchStatus = 0;
                }
                updatePanel = true;
                    UpdateFontComboBox();
                    UpdateFontSizeComboBox();
                    UpdateColorComboBox();
                    UpdateBackgroundColorComboBox();
                    UpdateBold();
                    UpdateItalic();
                    UpdateUnderline();
                    updatePanel = false;
            }
        }

        private Point startPoint;
        private bool isResizing = false;

        private void RichTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                startPoint = e.GetPosition(richTextBox);
                isResizing = true;
            }
        }

        private void RichTextBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (isResizing)
            {
                Point currentPoint = e.GetPosition(richTextBox);
                double widthChange = currentPoint.X - startPoint.X;
                double heightChange = currentPoint.Y - startPoint.Y;

                // Получаем текущий элемент под курсором
                TextPointer position = richTextBox.GetPositionFromPoint(currentPoint, true);
                Inline currentInline = position?.Parent as Inline;

                if (currentInline != null)
                {
                    // Обновляем шрифт в FontComboBox
                    FontComboBox.SelectedItem = Fonts.SystemFontFamilies.FirstOrDefault(font => font.Source == (currentInline.FontFamily ?? richTextBox.FontFamily).Source);

                    // Обновляем размер шрифта в FontSizeComboBox
                    FontSizeComboBox.SelectedItem = currentInline.FontSize;
                }

                // Ваш код для изменения размера контейнера (например, изменение ширины и высоты)
                ResizeContainer(widthChange, heightChange);

                // Обновляем startPoint
                startPoint = currentPoint;
            }
        }

        private void ResizeContainer(double widthChange, double heightChange)
        {
            InlineUIContainer selectedContainer = GetSelectedImageContainer();

            if (selectedContainer != null)
            {
                Image image = selectedContainer.Child as Image;

                if (image != null)
                {
                    try
                    {
                        // Изменение размера изображения
                        image.Width += widthChange;
                        image.Height += heightChange;
                    }
                    catch { }
                }
            }
        }

        private InlineUIContainer GetSelectedImageContainer()
        {
            TextPointer selectionStart = richTextBox.Selection.Start;
            TextPointer selectionEnd = richTextBox.Selection.End;

            if (selectionStart != null && selectionEnd != null)
            {
                TextRange selectedTextRange = new TextRange(selectionStart, selectionEnd);

                foreach (Inline inline in selectedTextRange.Start.Paragraph.Inlines)
                {
                    if (inline is InlineUIContainer inlineUIContainer && inlineUIContainer.Child is Image)
                    {
                        return inlineUIContainer;
                    }
                }
            }

            return null;
        }

        private void RichTextBox_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isResizing = false;
            }
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Scale = e.NewValue;
            ApplyScale();
            UpdateScaleValueLabel();
        }
        private void UpdateScaleValueLabel()
        {
            scaleValueLabel.Text = $"{Scale*100:F0}%";
        }

        private void ApplyScale()
        {
            richTextBox.LayoutTransform = new ScaleTransform(Scale, Scale);
        }

        private void InitializeDocument()
        {
            flowDocument = new FlowDocument();
            AddNewPage();
            richTextBox.Document = flowDocument;
        }
        private void AddNewPage()
        {

            
            // Створення нового RichTextBox
            RichTextBox newRichTextBox = new RichTextBox();
            newRichTextBox.AcceptsReturn = true;

            // Налаштування властивостей нового RichTextBox
            newRichTextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            newRichTextBox.Document = new FlowDocument();

            // Створення секції для нової сторінки
            Section section = new Section();
            newRichTextBox.Document.Blocks.Add(section);

            // Додавання нового RichTextBox до вмісту FlowDocument
            flowDocument.Blocks.Add(new BlockUIContainer(newRichTextBox));

            // Встановлення поточного RichTextBox
            currentRichTextBox = newRichTextBox;
        }

        private void NextPage()
        {
            if (currentPageIndex < flowDocument.Blocks.Count - 1)
            {
                currentPageIndex++;
                UpdateMainWindowTitle();
            }
        }

        private void PreviousPage()
        {
            if (currentPageIndex > 0)
            {
                currentPageIndex--;
                UpdateMainWindowTitle();
            }
        }


        private void UpdateMainWindowTitle()
        {
            // Формируйте заголовок окна с учетом текущего имени файла
            if (string.IsNullOrEmpty(currentFileName))
            {
                this.Title = "Text Editor";
            }
            else
            {
                this.Title = $"Text Editor - {System.IO.Path.GetFileName(currentFileName)} (Page {currentPageIndex + 1})";
            }
        }
        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (IfNotSave()) return;
            
            // Створення FlowDocument зі специфікаціями A4
            FlowDocument flowDocument = new FlowDocument();
            flowDocument.ColumnWidth = 8.27 * 96; // Ширина колонки в пікселях (A4 ширина в дюймах помножити на 96 пікселів)
            flowDocument.PageHeight = 11.69 * 96; // Висота сторінки в пікселях
            double fontSize = 12.0;
            // Створення секції для першої сторінки
            Section section = new Section();
            flowDocument.Blocks.Add(section);
            Paragraph paragraph = new Paragraph(new Run(""));
            paragraph.LineHeight = fontSize*0.5;
            //paragraph.
            section.Blocks.Add(paragraph);
            richTextBox.Document = flowDocument;
            richTextBox.IsEnabled = true;
            richTextBox.Visibility = Visibility.Visible;
            richTextBox.Focus();
            currentFileName = "Новий документ.";
            UpdateMainWindowTitle();
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            NextPage();
        }

        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            PreviousPage();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DocumentsHandler.SaveDocument(sender, e, currentFileName, richTextBox);
            isDirty = false;
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            currentFileName = DocumentsHandler.SaveDocumentAs(currentFileName, richTextBox);
            UpdateMainWindowTitle();
            isDirty = false;
        }


        private void Open_Click(object sender, RoutedEventArgs e) //LoadDocument(string filePath)
        {
            if (IfNotSave()) return;

            //richTextBox.Document = new FlowDocument(); // Очищаем содержимое RichTextBox
            currentFileName = DocumentsHandler.Open(richTextBox, currentFileName);
            UpdateMainWindowTitle();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (IfNotSave()) return;

            richTextBox.Document = new FlowDocument(); // Очищаем содержимое RichTextBox
            currentFileName = null; // Сбрасываем текущее имя файла

            // Деактивируем и скрываем RichTextBox при закрытии файла
            richTextBox.IsEnabled = false; 
            richTextBox.Visibility = Visibility.Collapsed;
            UpdateMainWindowTitle();
            isDirty = false;
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (IfNotSave())  return;
            
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IfNotSave()) e.Cancel = true; 
        }

        private bool IfNotSave()
        {
            if (isDirty)
            {
                MessageBoxResult result = MessageBox.Show("The text has been changed. Are you sure you want to close without saving?", "Warning!", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                    return true;
            }
            return false;
        }

        private void searchButton_Click(object sender, RoutedEventArgs e)
        {
            string temp1 = Interaction.InputBox("Which word do you want to find?", "Input", "");
            if (!string.IsNullOrEmpty(temp1))
            {
                // Видаляємо попередні виділення перед новим пошуком
                foreach (TextRange range in highlightedRanges)
                {
                    range.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
                }
                highlightedRanges.Clear();

                FindAndUnderlineWord(richTextBox, temp1);
                searchStatus = 1;
            }
        }

        private void FindAndUnderlineWord(RichTextBox richTextBox, string wordToFind)
        {
            TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
            TextPointer current = textRange.Start.GetInsertionPosition(LogicalDirection.Forward);

            while (current != null)
            {
                string textInRun = current.GetTextInRun(LogicalDirection.Forward);
                int index = textInRun.IndexOf(wordToFind, StringComparison.CurrentCultureIgnoreCase);

                if (index != -1)
                {
                    TextPointer start = current.GetPositionAtOffset(index);
                    TextPointer end = start.GetPositionAtOffset(wordToFind.Length);
                    TextRange wordRange = new TextRange(start, end);
                    wordRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.LightGreen);

                    // Зберігаємо діапазон, щоб потім відновити фон
                    highlightedRanges.Add(wordRange);

                    current = end;
                }
                else
                {
                    current = current.GetNextContextPosition(LogicalDirection.Forward);
                }
            }

            
        }

        private void ClearSearch()
        {
                // Відновлюємо фон для всіх збережених діапазонів
                foreach (TextRange range in highlightedRanges)
                {
                    range.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
                }
                highlightedRanges.Clear();
        }


        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            richTextBox.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, (FontWeight)richTextBox.Selection.GetPropertyValue(TextElement.FontWeightProperty) == FontWeights.Bold ? FontWeights.Normal : FontWeights.Bold);

            // Обновляем фон кнопки в соответствии с наличием выделения жирного шрифта
            UpdateBold();
        }

        private void UpdateBold()
        {
            try
            {
                FontWeight fontWeight = (FontWeight)richTextBox.Selection.GetPropertyValue(TextElement.FontWeightProperty);

                if (fontWeight == FontWeights.Bold)
                {
                    boldButton.Background = Brushes.DarkGray;
                }
                else
                {
                    boldButton.ClearValue(Button.BackgroundProperty);
                }
            }
            catch { }
        }
        private void UpdateItalic()
        {
            try
            {
                FontStyle fontStyle = (FontStyle)richTextBox.Selection.GetPropertyValue(TextElement.FontStyleProperty);

                if (fontStyle == FontStyles.Italic)
                {
                    italicButton.Background = Brushes.DarkGray;
                }
                else
                {
                    italicButton.ClearValue(Button.BackgroundProperty);
                }
            }
            catch { }
        }
        private void UpdateUnderline()
        {
            try
            {
                // Обновляем фон кнопки в соответствии с наличием подчеркивания
                TextDecorationCollection textDecorations = (TextDecorationCollection)richTextBox.Selection.GetPropertyValue(Inline.TextDecorationsProperty);

                if (textDecorations != null && textDecorations.Contains(TextDecorations.Underline[0]))
                {
                    underlineButton.Background = Brushes.DarkGray;
                }
                else
                {
                    underlineButton.ClearValue(Button.BackgroundProperty);
                }
            }
            catch { }
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            richTextBox.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, (FontStyle)richTextBox.Selection.GetPropertyValue(TextElement.FontStyleProperty) == FontStyles.Italic ? FontStyles.Normal : FontStyles.Italic);
            UpdateItalic();
        }

        private void Underline_Click(object sender, RoutedEventArgs e)
        {
            richTextBox.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, (TextDecorationCollection)richTextBox.Selection.GetPropertyValue(Inline.TextDecorationsProperty) == System.Windows.TextDecorations.Underline ? null : System.Windows.TextDecorations.Underline);
            UpdateUnderline();
        }

        private void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!updatePanel)
            {
                if (FontComboBox.SelectedItem != null)
                {
                    string selectedFont = ((ComboBoxItem)FontComboBox.SelectedItem).Content.ToString();
                    ApplyToSelection(TextElement.FontFamilyProperty, new FontFamily(selectedFont));
                }

            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!updatePanel)
            {
                if (FontSizeComboBox.SelectedItem != null)
                {
                    string selectedFontSize = ((ComboBoxItem)FontSizeComboBox.SelectedItem).Content.ToString();
                    ApplyToSelection(TextElement.FontSizeProperty, double.Parse(selectedFontSize));
                }
            }
        }

        private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!updatePanel)
            {
                if (ColorComboBox.SelectedItem != null)
                {
                    try
                    {
                        // Получаем выбранный цвет
                        Color selectedColor = (Color)ColorComboBox.SelectedItem;

                        // Создаем SolidColorBrush с использованием полученного цвета
                        SolidColorBrush solidColorBrush = new SolidColorBrush(selectedColor);

                        // Применяем к выделенному тексту
                        ApplyToSelection(TextElement.ForegroundProperty, solidColorBrush);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void BackgroundColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!updatePanel)
            {
                if (BackgroundColorComboBox.SelectedItem != null)
                {
                    try
                    {
                        // Получаем выбранный цвет
                        Color selectedColor = (Color)BackgroundColorComboBox.SelectedItem;

                        // Создаем SolidColorBrush с использованием полученного цвета
                        SolidColorBrush solidColorBrush = new SolidColorBrush(selectedColor);

                        // Применяем к выделенному тексту
                        ApplyToSelection(TextElement.BackgroundProperty, solidColorBrush);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void sizeUp_Click(object sender, RoutedEventArgs e)
        {
            ChangeFontSize(2);
            UpdateFontSizeComboBox();
        }

        private void sizeDown_Click(object sender, RoutedEventArgs e)
        {
            ChangeFontSize(-2);
            UpdateFontSizeComboBox();
        }

        private void UpdateFontSizeComboBox()
        {
            // Обновляем размер шрифта в FontSizeComboBox
            string FSize = richTextBox.Selection.GetPropertyValue(TextElement.FontSizeProperty).ToString();

            foreach (ComboBoxItem item in FontSizeComboBox.Items)
            {
                if (item.Content.ToString() == FSize)
                {
                    FontSizeComboBox.SelectedItem = item;
                    break;
                }
            }
        }
        private void UpdateFontComboBox()
        {
            // Обновляем размер шрифта в FontSizeComboBox
            string FFamily = richTextBox.Selection.GetPropertyValue(TextElement.FontFamilyProperty).ToString();

            foreach (ComboBoxItem item in FontComboBox.Items)
            {
                if (item.Content.ToString() == FFamily)
                {
                    FontComboBox.SelectedItem = item;
                    break;
                }
            }
        }
        private void UpdateColorComboBox()
        {
            object foregroundValue = richTextBox.Selection.GetPropertyValue(TextElement.ForegroundProperty);

            if (foregroundValue is SolidColorBrush solidColorBrush)
            {
                // Преобразуем цвет в строку
                string textColor = solidColorBrush.ToString();

                // Обновляем цвет в ColorComboBox
                foreach (object item in ColorComboBox.Items)
                {
                    if (item.ToString() == textColor)
                    {
                        ColorComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        private void UpdateBackgroundColorComboBox()
        {
            //object backgroundValue = GetBackgroundBeforeCaret();
            Paragraph currentParagraph = richTextBox.CaretPosition.Paragraph;

            if (currentParagraph != null && currentParagraph.Inlines.Count > 0)
            {
                Inline lastInline = currentParagraph.Inlines.LastInline;

                if (lastInline != null)
                {
                    // Получаем значение свойства Background
                    object backgroundValue = lastInline.GetValue(Inline.BackgroundProperty);

                    if (backgroundValue is SolidColorBrush solidColorBrush)
                    {
                        // Преобразуем цвет в строку
                        string textBackgroundColor = solidColorBrush.ToString();

                        // Обновляем цвет в BackgroundColorComboBox
                        foreach (object item in BackgroundColorComboBox.Items)
                        {
                            if (item.ToString() == textBackgroundColor)
                            {
                                BackgroundColorComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    else
                    {
                        string textBackgroundColor = "#FFFFFFFF";
                        foreach (object item in BackgroundColorComboBox.Items)
                        {
                            if (item.ToString() == textBackgroundColor)
                            {
                                BackgroundColorComboBox.SelectedItem = item;
                                break;
                            }
                        }

                    }
                }
            }
        }
        

        private void ChangeFontSize(double delta)
        {
            if (!richTextBox.Selection.IsEmpty)
            {
                richTextBox.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, richTextBox.Selection.GetPropertyValue(TextElement.FontSizeProperty) is double currentSize
                    ? (object)(currentSize + delta)
                    : delta);
            }
        }
        private void ApplyToSelection(DependencyProperty property, object value)
        {
            TextRange selection = new TextRange(richTextBox.Selection.Start, richTextBox.Selection.End);

            if (selection.IsEmpty)
            {
                Paragraph paragraph = richTextBox.CaretPosition.Paragraph;
                if (paragraph != null)
                {
                    ApplyPropertyValueToParagraph(paragraph, property, value);
                }
            }
            else
            {
                ApplyPropertyValueToTextRange(selection, property, value);
            }
        }

        private void ApplyPropertyValueToParagraph(Paragraph paragraph, DependencyProperty property, object value)
        {
            paragraph.SetValue(property, value);
        }

        private void ApplyPropertyValueToTextRange(TextRange range, DependencyProperty property, object value)
        {
            if (range != null)
            {
                range.ApplyPropertyValue(property, value);

                TextPointer start = range.Start.GetPositionAtOffset(1);
                TextPointer end = range.End.GetPositionAtOffset(-1);

                if (start != null && end != null)
                {
                    TextRange modifiedRange = new TextRange(start, end);
                    modifiedRange.ApplyPropertyValue(property, value);
                }
            }
        }

        private void RichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveState();
            MarkAsDirty();
            // Перевірка, чи текст виходить за межі поточного RichTextBox
            if (currentRichTextBox != null && currentRichTextBox.Document.Blocks.FirstBlock != null)
            {
                var lastParagraph = currentRichTextBox.Document.Blocks.LastBlock as Paragraph;

                if (lastParagraph != null && lastParagraph.ContentEnd.GetLineStartPosition(0).IsAtLineStartPosition == false)
                {
                    // Якщо текст виходить за межі, додати нову сторінку
                    AddNewPage();
                }
            }
        }

        private void SaveState()
        {
            // Зберегти поточний текстовий стан
            TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
            undoStack.Push(textRange.Text);

            // Очистіть стек відновлення, оскільки нова зміна тексту знімає можливість відновлення попередніх дій
            redoStack.Clear();
        }
        private void ChangeCase_Click(object sender, RoutedEventArgs e)
        {
            if (!richTextBox.Selection.IsEmpty)
            {
                string selectedText = richTextBox.Selection.Text;

                // Перевірте поточний регістр та застосуйте протилежний
                if (selectedText == selectedText.ToLower())
                {
                    richTextBox.Selection.Text = selectedText.ToUpper();
                }
                else
                {
                    richTextBox.Selection.Text = selectedText.ToLower();
                }
            }
        }
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count > 0)
            {
                // Відмінити: зберегти поточний стан перед відміною
                SaveState();

                // Відновити попередній стан
                string previousState = undoStack.Pop();
                SetText(previousState);

                // Додайте поточний стан до стеку відновлення
                redoStack.Push(previousState);
            }
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (redoStack.Count > 0)
            {
                // Відновити: зберегти поточний стан перед відновленням
                SaveState();

                // Відновити наступний стан
                string nextState = redoStack.Pop();
                SetText(nextState);

                // Додайте поточний стан до стеку відміни
                undoStack.Push(nextState);
            }
        }

        private void SetText(string text)
        {
            // Встановити текстовий зміст
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(new Paragraph(new Run(text)));
        }

        private void alignLeft_Click(object sender, RoutedEventArgs e)
        {
            richTextBox.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, TextAlignment.Left);
        }

        private void alignCentr_Click(object sender, RoutedEventArgs e)
        {
            richTextBox.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, TextAlignment.Center);
        }

        private void alignRight_Click(object sender, RoutedEventArgs e)
        {
            richTextBox.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, TextAlignment.Right);
        }

        private void alignJustify_Click(object sender, RoutedEventArgs e)
        {
            richTextBox.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, TextAlignment.Justify);
        }

        private void insertPicButton_Click(object sender, RoutedEventArgs e)
        {

            // Проверка, открыт ли документ
            if (richTextBox.CaretPosition.Paragraph != null)
            {
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
                openFileDialog.Filter = "Изображения (*.png; *.jpg; *.jpeg; *.gif; *.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Все файлы (*.*)|*.*";

                if (openFileDialog.ShowDialog() == true)
                {
                    // Получение пути к выбранному файлу
                    string imagePath = openFileDialog.FileName;

                    // Получение текущей ширины страницы
                    double pageWidth = richTextBox.ExtentWidth;
                    double newWidth;
                    double newHeight;
                    // Создание объекта BitmapImage
                    BitmapImage bitmap = new BitmapImage(new Uri(imagePath));

                    // Вычисление пропорционального размера изображения
                    double aspectRatio = bitmap.Width / bitmap.Height;
                    if (bitmap.Width > pageWidth)
                    {
                        newWidth = pageWidth; // новая ширина равна ширине страницы
                        newHeight = newWidth / aspectRatio; // новая высота с учетом соотношения сторон
                    }
                    else
                    {
                        newWidth = bitmap.Width;
                        newHeight = bitmap.Height;
                    }
                    // Создание элемента Image
                    Image image = new Image();
                    image.Source = bitmap;
                    image.Width = newWidth;
                    image.Height = newHeight;

                    // Создание контейнера для вставки изображения в RichTextBox
                    InlineUIContainer container = new InlineUIContainer(image, richTextBox.CaretPosition);

                    richTextBox.CaretPosition.Paragraph.Inlines.Add(container);
                }
            }
            else
            {
                MessageBox.Show("The document is not open. Open your document before inserting the image.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            DocumentsHandler.SaveDocument(sender, e, currentFileName, richTextBox);
            isDirty = false;
        }

        private void rotateButton_Click(object sender, RoutedEventArgs e)
        {
            if (richTextBox.Width == 595)
            {
                richTextBox.Height = 595;
                richTextBox.Width = 842;
                richTextBox.Document.PageWidth = 842;
                richTextBox.Document.PageHeight = 595;

            }
            else
            {
                richTextBox.Height = 842;
                richTextBox.Width = 595;
                richTextBox.Document.PageWidth = 595;
                richTextBox.Document.PageHeight = 842;
            }
        }

        private void zoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            Scale -= 0.01;
            scaleSlider.SetBinding(Slider.ValueProperty, binding);
        }

        private void zoomInButton_Click(object sender, RoutedEventArgs e)
        {
            Scale += 0.01;
            scaleSlider.SetBinding(Slider.ValueProperty, binding);
        }

        private void helpButton_Click(object sender, RoutedEventArgs e)
        {

            string pdfFilePath = "TextEditor-manual.pdf";


            if (System.IO.File.Exists(pdfFilePath))
            {

                MessageBoxResult result = MessageBox.Show("Open guide page?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {

                    string browserPath = @"C:\Program Files\Internet Explorer\iexplore.exe";


                    string pdfUrl = "file:///" + System.IO.Path.GetFullPath(pdfFilePath).Replace("\\", "/");


                    Process.Start(browserPath, pdfUrl);
                }
            }
            else
            {
                MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void markerBullet_Click(object sender, RoutedEventArgs e)
        {
            ApplyToList(listType: TextMarkerStyle.Disc);
        }

        private void markerNumbered_Click(object sender, RoutedEventArgs e)
        {
            ApplyToList(listType: TextMarkerStyle.Decimal);
        }

        private void ApplyToList(TextMarkerStyle listType)
        {
            string bulletSymbol = listType == TextMarkerStyle.Disc ? "•" : "1";

            TextSelection selectedText = richTextBox.Selection;

            if (!selectedText.IsEmpty)
            {
                string[] lines = selectedText.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                bool hasBullet = lines.Any(line => line.TrimStart().StartsWith(bulletSymbol));

                selectedText.Text = string.Empty;

                foreach (string line in lines)
                {
                    if (hasBullet)
                    {
                        string trimmedLine = line.TrimStart(bulletSymbol.ToCharArray());
                        selectedText.Text += trimmedLine.TrimStart() + Environment.NewLine;
                    }
                    else
                    {
                        selectedText.Text += bulletSymbol + " " + line + Environment.NewLine;
                    }
                }
            }
            else
            {
                try
                {
                    Paragraph paragraph = richTextBox.Document.Blocks.LastBlock as Paragraph;

                    if (paragraph != null)
                    {
                        string paragraphText = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
                        bool hasBullet = paragraphText.TrimStart().StartsWith(bulletSymbol);

                        Paragraph previousParagraph = GetPreviousParagraph(paragraph);
                        bool startsWithNumberAndDot = previousParagraph != null && Regex.IsMatch(previousParagraph.Inlines.FirstInline?.ContentStart.GetTextInRun(LogicalDirection.Forward), @"^\d+\.");

                        if (hasBullet && startsWithNumberAndDot)
                        {
                            // Відступаємо вправо на 3 символи
                            TextPointer paragraphStart = paragraph.ContentStart.GetPositionAtOffset(0);
                            TextPointer newCaretPosition = paragraphStart.GetPositionAtOffset(3, LogicalDirection.Forward);
                            richTextBox.CaretPosition = newCaretPosition;
                        }

                        if (hasBullet)
                        {
                            paragraph.Inlines.Clear();
                            paragraph.Inlines.Add(new Run(paragraphText.TrimStart(bulletSymbol.ToCharArray()).TrimStart()));
                        }
                        else
                        {
                            paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline, new Run(bulletSymbol + " "));
                        }
                    }
                    else
                    {
                        paragraph = new Paragraph(new Run(bulletSymbol + " "));
                        richTextBox.Document.Blocks.Add(paragraph);
                    }
                }
                catch (ArgumentNullException ex)
                {
                    Console.WriteLine($"ArgumentNullException: {ex.Message}");
                    Console.WriteLine($"Parameter name: {ex.ParamName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                }
            }
        }

        private Paragraph GetPreviousParagraph(Paragraph currentParagraph)
        {
            if (currentParagraph == null)
            {
                return null;
            }

            var blockCollection = richTextBox.Document.Blocks;
            var enumerator = blockCollection.GetEnumerator();

            Paragraph previousParagraph = null;

            while (enumerator.MoveNext())
            {
                var currentBlock = enumerator.Current as Paragraph;

                if (currentBlock == currentParagraph)
                {
                    return previousParagraph;
                }

                previousParagraph = currentBlock;
            }

            return null;
        }


        private void markerButton_Click(object sender, RoutedEventArgs e)
        {
            TextRange selectedRange = new TextRange(richTextBox.Selection.Start, richTextBox.Selection.End);

            if (selectedRange.Text.Length > 0)
            {
                string selectedText = selectedRange.Text;
                string[] lines = selectedText.Split(new[] { "\r\n" }, StringSplitOptions.None);

                bool allLinesNumbered = lines.All(line => IsLineNumbered(line));
                bool anyLinesNumbered = lines.Any(line => IsLineNumbered(line));

                if (allLinesNumbered)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        lines[i] = RemoveNumbering(lines[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (anyLinesNumbered)
                        {
                            lines[i] = RemoveNumbering(lines[i]);
                        }
                        lines[i] = (i + 1).ToString() + ". " + lines[i];
                    }
                }

                string updatedText = String.Join("\r\n", lines);
                selectedRange.Text = updatedText;

                UpdateFollowingLinesNumbering();

                richTextBox.Selection.Select(selectedRange.Start, selectedRange.Start);
            }
        }



        private bool IsLineNumbered(string line)
        {
            return line.Length > 2 && Char.IsDigit(line[0]) && line[1] == '.';
        }

        private string RemoveNumbering(string line)
        {
            int firstSpaceIndex = line.IndexOf(' ');
            return firstSpaceIndex != -1 ? line.Substring(firstSpaceIndex + 1) : line;
        }

        private void UpdateFollowingLinesNumbering()
        {
            TextRange afterRange = new TextRange(richTextBox.Selection.End, richTextBox.Document.ContentEnd);
            string[] afterLines = afterRange.Text.Split(new[] { "\r\n" }, StringSplitOptions.None);

            bool numberingExistsInAfterLines = afterLines.Length > 0 && afterLines[0].Length > 2 && afterLines[0][1] == '.';

            if (numberingExistsInAfterLines)
            {
                for (int i = 0; i < afterLines.Length; i++)
                {
                    if (afterLines[i].Length > 2 && afterLines[i][1] == '.' && Char.IsDigit(afterLines[i][0]))
                    {
                        afterLines[i] = (i + 1).ToString() + ". " + afterLines[i].Substring(afterLines[i].IndexOf(' ') + 1);
                    }
                }

                string updatedAfterText = String.Join("\r\n", afterLines);
                afterRange.Text = updatedAfterText;
            }
        }

    }
}