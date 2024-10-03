using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows;

namespace TextEditor.FileSystem
{
    public class DocumentsHandler
    {
        public static void SaveDocument(object sender, RoutedEventArgs e, string currentFileName, RichTextBox richTextBox)
        {
            if (!string.IsNullOrEmpty(currentFileName) && currentFileName != "Новий документ.")
            {
                try
                {
                    string fileExtension = System.IO.Path.GetExtension(currentFileName).ToLower();

                    switch (fileExtension)
                    {
                        case ".txt":
                            SaveTextFile(currentFileName, richTextBox);
                            break;
                        case ".rtf":
                            SaveRtfFile(currentFileName, richTextBox);
                            break;
                        case ".pdf":
                            SavePdfFile(currentFileName);
                            break;
                        default:
                            MessageBox.Show("Unsupported file format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                SaveDocumentAs(currentFileName, richTextBox);
            }
        }

        public static void SaveTextFile(string filePath, RichTextBox richTextBox)
        {
            string content = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd).Text;
            File.WriteAllText(filePath, content);
        }

        public static void SaveRtfFile(string filePath, RichTextBox richTextBox)
        {
            TextRange range = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                range.Save(fs, DataFormats.Rtf);
            }
        }
        public static void SavePdfFile(string filePath)
        {
            //PdfSharp.Pdf.PdfDocument pdfDocument = new PdfSharp.Pdf.PdfDocument();
            //PdfPage pdfPage = pdfDocument.AddPage();
            //XGraphics gfx = XGraphics.FromPdfPage(pdfPage);
            //XTextFormatter tf = new XTextFormatter(gfx);

            //string content = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd).Text;
            //tf.DrawString(content, new XFont("Arial", 12), XBrushes.Black, new XRect(10, 10, pdfPage.Width.Point, pdfPage.Height.Point), XStringFormats.TopLeft);

            //pdfDocument.Save(filePath);
        }

        public static string SaveDocumentAs(string currentFileName, RichTextBox richTextBox)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text files (*.txt)|*.txt|Rich Text Format (*.rtf)|*.rtf|All files (*.*)|*.*";

            if (!string.IsNullOrEmpty(currentFileName))
            {
                saveFileDialog.FileName = currentFileName;
            }

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = saveFileDialog.FileName;
                    string content = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd).Text;

                    // Определение выбранного формата
                    string fileExtension = System.IO.Path.GetExtension(filePath);

                    if (fileExtension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        // Сохранение в формате txt
                        File.WriteAllText(filePath, content);
                    }
                    else if (fileExtension.Equals(".rtf", StringComparison.OrdinalIgnoreCase))
                    {
                        // Сохранение в формате rtf
                        TextRange range = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
                        using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            range.Save(fileStream, DataFormats.Rtf);
                        }
                    }
                    else if (fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    {

                    }

                    currentFileName = filePath; // Обновляем текущее имя файла
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            return currentFileName;
        }


        public static string Open(RichTextBox richTextBox, string currentFileName)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                string fileExtension = System.IO.Path.GetExtension(openFileDialog.FileName).ToLower();

                try
                {
                    switch (fileExtension)
                    {
                        case ".txt":
                            currentFileName = OpenTextFile(openFileDialog.FileName, richTextBox, currentFileName);
                            break;
                        case ".rtf":
                            currentFileName = OpenRtfFile(openFileDialog.FileName, richTextBox, currentFileName);
                            break;
                        case ".pdf":
                            //OpenPdfFile(openFileDialog.FileName);
                            break;
                        default:
                            MessageBox.Show("Unsupported file format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                    }
                    richTextBox.IsEnabled = true;
                    richTextBox.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            return currentFileName;
        }


        public static string OpenTextFile(string filePath, RichTextBox richTextBox, string currentFileName)
        {
            string content = File.ReadAllText(filePath);
            richTextBox.Document = new FlowDocument(new Paragraph(new Run(content)));
            currentFileName = filePath;
            return currentFileName;
        }

        public static string OpenRtfFile(string filePath, RichTextBox richTextBox, string currentFileName)
        {
            TextRange range = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            {
                range.Load(fs, DataFormats.Rtf);
            }
            currentFileName = filePath;
            return currentFileName;
        }
    }
}
