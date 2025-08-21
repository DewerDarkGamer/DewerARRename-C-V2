using System.Drawing;
using ZXing;
using ZXing.Windows.Compatibility;

namespace BarcodeRename
{
    public partial class MainForm : Form
    {
        private readonly BarcodeReader _reader;
        private readonly ListBox _logListBox;
        private const int MIN_BARCODE_LENGTH = 10;
        private const int MAX_BARCODE_LENGTH = 12;

        public MainForm()
        {
            InitializeComponent();

            this.Size = new Size(800, 600);
            this.Text = "Barcode Rename";
            this.MinimumSize = new Size(600, 400);

            _logListBox = new ListBox
            {
                Dock = DockStyle.Bottom,
                Height = 400
            };

            // สร้าง BarcodeReader
            _reader = new BarcodeReader
            {
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat>
                    {
                        BarcodeFormat.CODE_128,
                        BarcodeFormat.CODE_39,
                        BarcodeFormat.QR_CODE,
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.ITF
                    }
                }
            };

            // สร้าง Panel สำหรับปุ่ม
            var buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 60,
                Padding = new Padding(10),
                ColumnCount = 2,
                RowCount = 1
            };

            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var btnSelectFiles = new Button
            {
                Text = "Select Files",
                Dock = DockStyle.Fill,
                Height = 40,
                Font = new Font(Font.FontFamily, 10, FontStyle.Regular),
                Margin = new Padding(5)
            };
            btnSelectFiles.Click += (_, _) => SelectFiles();

            var btnSelectFolder = new Button
            {
                Text = "Select Folder",
                Dock = DockStyle.Fill,
                Height = 40,
                Font = new Font(Font.FontFamily, 10, FontStyle.Regular),
                Margin = new Padding(5)
            };
            btnSelectFolder.Click += (_, _) => SelectFolder();

            buttonPanel.Controls.Add(btnSelectFiles, 0, 0);
            buttonPanel.Controls.Add(btnSelectFolder, 1, 0);

            // เพิ่ม Controls เข้าฟอร์ม
            this.Controls.Add(_logListBox);
            this.Controls.Add(buttonPanel);

            // แสดงข้อความต้อนรับ
            _logListBox.Items.Add("Welcome to Barcode Rename");
            _logListBox.Items.Add("Click 'Select Files' to process individual files or 'Select Folder' to process all images in a folder");
            _logListBox.Items.Add($"Barcode length requirement: {MIN_BARCODE_LENGTH}-{MAX_BARCODE_LENGTH} characters");
            _logListBox.Items.Add("");
        }

        private void SelectFiles()
        {
            using var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image files (*.png;*.jpeg;*.jpg;*.bmp)|*.png;*.jpeg;*.jpg;*.bmp|All files (*.*)|*.*",
                FilterIndex = 1,
                Title = "Select Images to Rename"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                ProcessFiles(openFileDialog.FileNames);
            }
        }

        private void SelectFolder()
        {
            using var folderDialog = new FolderBrowserDialog
            {
                Description = "Select Folder Containing Images",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                string[] files = Directory.GetFiles(folderDialog.SelectedPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => file.ToLower().EndsWith(".png") || 
                                 file.ToLower().EndsWith(".jpg") || 
                                 file.ToLower().EndsWith(".jpeg") || 
                                 file.ToLower().EndsWith(".bmp"))
                    .ToArray();

                if (files.Length == 0)
                {
                    _logListBox.Items.Add("No image files found in the selected folder");
                    return;
                }

                ProcessFiles(files);
            }
        }

        private void ProcessFiles(string[] files)
{
    _logListBox.Items.Clear();

    foreach (string filePath in files)
    {
        try
        {
            List<string> barcodes;
            using (var bitmap = new Bitmap(filePath))
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, bitmap.RawFormat);
                using var tempBitmap = new Bitmap(ms);
                barcodes = ReadAllBarcodes(tempBitmap);
            }

            if (barcodes.Any())
            {
                _logListBox.Items.Add($"Found {barcodes.Count} barcodes in: {Path.GetFileName(filePath)}");
                
                // กรองและแสดงเฉพาะ barcodes ที่มีความยาว 10-12 ตัวอักษร
                var validBarcodes = barcodes
                    .Where(b => b.Length >= MIN_BARCODE_LENGTH && b.Length <= MAX_BARCODE_LENGTH)
                    .ToList();

                foreach (var barcode in barcodes)
                {
                    bool isValid = barcode.Length >= MIN_BARCODE_LENGTH && barcode.Length <= MAX_BARCODE_LENGTH;
                    _logListBox.Items.Add($"  - {barcode} (Length: {barcode.Length}) {(isValid ? "[VALID]" : "[INVALID]")}");
                }

                if (validBarcodes.Any())
                {
                    // เลือก barcode ที่ถูกต้องตัวแรก
                    string selectedBarcode = validBarcodes[0];
                    string directory = Path.GetDirectoryName(filePath)!;
                    string extension = Path.GetExtension(filePath);
                    string currentFileName = Path.GetFileNameWithoutExtension(filePath);
                    string newFileName = selectedBarcode;
                    string newFilePath = Path.Combine(directory, $"{newFileName}{extension}");

                    // ตรวจสอบว่าชื่อไฟล์เดิมตรงกับ barcode
                    if (currentFileName.Equals(newFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logListBox.Items.Add($"Skipped: File already has correct name - {Path.GetFileName(filePath)}");
                        continue;
                    }

                    // ถ้ามีไฟล์อยู่แล้ว ให้เพิ่มตัวเลขต่อท้าย
                    int counter = 1;
                    while (File.Exists(newFilePath))
                    {
                        newFilePath = Path.Combine(directory, $"{newFileName}_{counter}{extension}");
                        counter++;
                    }

                    Thread.Sleep(100);
                    File.Move(filePath, newFilePath);
                    _logListBox.Items.Add($"Renamed: {Path.GetFileName(filePath)} -> {Path.GetFileName(newFilePath)}");
                }
                else
                {
                    _logListBox.Items.Add($"Skipped: No barcode meets length requirement ({MIN_BARCODE_LENGTH}-{MAX_BARCODE_LENGTH} characters)");
                }
            }
            else
            {
                _logListBox.Items.Add($"No barcode found in: {Path.GetFileName(filePath)}");
            }

            _logListBox.Items.Add(""); // เพิ่มบรรทัดว่างระหว่างไฟล์
        }
        catch (Exception ex)
        {
            _logListBox.Items.Add($"Error processing {Path.GetFileName(filePath)}: {ex.Message}");
            _logListBox.Items.Add(""); // เพิ่มบรรทัดว่างหลังข้อผิดพลาด
        }
    }

    // แสดงสรุปที่ด้านล่างของล็อก
    _logListBox.Items.Add("");
    _logListBox.Items.Add($"Process completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    _logListBox.Items.Add($"Barcode length requirement: {MIN_BARCODE_LENGTH}-{MAX_BARCODE_LENGTH} characters");
}
        private List<string> ReadAllBarcodes(Bitmap image)
        {
            try
            {
                var barcodes = new List<string>();
                var results = _reader.DecodeMultiple(image);
                
                if (results != null)
                {
                    foreach (var result in results)
                    {
                        if (!string.IsNullOrWhiteSpace(result.Text))
                        {
                            barcodes.Add(result.Text);
                        }
                    }
                }

                return barcodes;
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
