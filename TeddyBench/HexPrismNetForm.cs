using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Ph0rd.ColorSampler;

namespace TeddyBench
{
    /// <summary>
    /// Creates a first-draft, one-piece net for a regular hexagonal prism.
    /// The second hexagonal end is deliberately omitted for now.
    /// </summary>
    public class HexPrismNetForm : Form
    {
        private const float Sqrt3 = 1.7320508075688772f;
        private const float A4PortraitWidthMm = 210f;
        private const float A4PortraitHeightMm = 297f;
        private const int A4MarginHundredthsInch = 20;
        private const float MillimetresPerHundredthInch = 0.254f;

        private readonly RadioButton radSideLength;
        private readonly RadioButton radVertexDiameter;
        private readonly RadioButton radEdgeDiameter;
        private readonly TextBox txtHexValue;
        private readonly TextBox txtFaceHeight;
        private readonly TextBox txtFaceTolerance;
        private readonly Label lblTolerance;
        private readonly CheckBox chkConnectFaces;
        private readonly NumericUpDown numHexagonCount;
        private readonly TabControl designTabs;
        private readonly Button btnAddDesign;
        private readonly Button btnImportDesign;
        private readonly Button btnExportDesign;
        private readonly Button btnDeleteDesign;
        private readonly TextBox txtDesignName;
        private readonly Button btnLineColor;
        private readonly Panel pnlLineColor;
        private readonly NumericUpDown numLineThickness;
        private readonly RadioButton radStandsOnVertex;
        private readonly RadioButton radStandsOnEdge;
        private readonly TextBox txtHeader;
        private readonly ComboBox cmbHeaderFont;
        private readonly ComboBox cmbHeaderFontSize;
        private readonly Button btnHeaderTextColor;
        private readonly Panel pnlHeaderTextColor;
        private readonly TextBox txtSubtext;
        private readonly ComboBox cmbSubtextFont;
        private readonly ComboBox cmbSubtextFontSize;
        private readonly Button btnSubtextTextColor;
        private readonly Panel pnlSubtextTextColor;
        private readonly Button btnAddImage;
        private readonly Button btnRemoveImage;
        private readonly Label lblImageName;
        private readonly NumericUpDown numHexagonImageScale;
        private readonly Button btnAddFaceImage;
        private readonly Button btnRemoveFaceImage;
        private readonly Label lblFaceImageName;
        private readonly NumericUpDown numFaceImageScale;
        private readonly CheckBox chkFaceImageFlip;
        private readonly TextBox txtFaceText;
        private readonly ToolTip faceTextToolTip;
        private readonly ComboBox cmbFaceTextFont;
        private readonly ComboBox cmbFaceTextFontSize;
        private readonly Button btnFaceTextColor;
        private readonly Panel pnlFaceTextColor;
        private readonly ComboBox cmbFaceTextAlignment;
        private readonly CheckBox chkFaceTextFlipTopToBottom;
        private readonly RadioButton radFaceTextRepeat;
        private readonly RadioButton radFaceTextWrapAround;
        private readonly RadioButton radFaceTextLinePerFace;
        private readonly ComboBox cmbFaceTextStartAt;
        private readonly Button btnHexagonBackgroundColor;
        private readonly Panel pnlHexagonBackgroundColor;
        private readonly Button btnFaceBackgroundColor;
        private readonly Panel pnlFaceBackgroundColor;
        private readonly Label lblConversions;
        private readonly Button btnCreate;
        private readonly Button btnPrint;
        private readonly Button btnLoadLayout;
        private readonly Button btnSaveLayout;
        private readonly NetPreviewPanel preview;
        private readonly PrintDocument printDocument;

        private float sideLengthMm = 25;
        private float lateralFaceHeightMm = 15;
        private float faceToleranceMm;
        private Color lineColor = Color.Gray;
        private Color headerTextColor = Color.Black;
        private Color subtextTextColor = Color.Black;
        private Color hexagonBackgroundColor = Color.White;
        private Color faceBackgroundColor = Color.White;
        private Image hexagonImage;
        private Image faceImage;
        private bool hasNet;
        private bool a4Landscape;
        private bool loadingDesign;
        private bool loadingLayout;
        private int activeDesignIndex;
        private readonly List<HexagonDesign> designs = new List<HexagonDesign>();
        private readonly Dictionary<string, Image> textImages = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private string layoutDirectory;
        private int draggedTabIndex = -1;
        private const string DesignFileFilter = "Hexagon design (*.hexagon)|*.hexagon|All files (*.*)|*.*";
        private Sampler activeColorSampler;

        public HexPrismNetForm()
        {
            Text = "Hexagonal prism net";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(820, 690);
            Size = new Size(960, 740);
            faceTextToolTip = new ToolTip { AutoPopDelay = 30000, InitialDelay = 100 };

            Panel settingsPanel = new Panel
            {
                Dock = DockStyle.Left,
                Padding = new Padding(14),
                Width = 310,
                AutoScroll = true
            };

            Label instructions = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 50,
                Text = "Enter one hexagon measurement and the height of each lateral face. All dimensions are in millimetres. The net is printed at true size on A4."
            };
            // Fit the label to its wrapped text so no gap is left below it.
            instructions.Resize += delegate
            {
                int height = TextRenderer.MeasureText(instructions.Text, instructions.Font, new Size(Math.Max(1, instructions.Width), Int32.MaxValue), TextFormatFlags.WordBreak).Height;
                if (instructions.Height != height)
                {
                    instructions.Height = height;
                }
            };

            GroupBox hexGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 155,
                Text = "Hexagon measurement"
            };
            radSideLength = new RadioButton { AutoSize = true, Location = new Point(12, 23), Text = "Side length", Checked = true };
            radVertexDiameter = new RadioButton { AutoSize = true, Location = new Point(12, 48), Text = "Diameter, vertex to vertex" };
            radEdgeDiameter = new RadioButton { AutoSize = true, Location = new Point(12, 73), Text = "Diameter, edge to edge" };
            Label valueLabel = new Label { AutoSize = true, Location = new Point(12, 106), Text = "Value (mm):" };
            txtHexValue = new TextBox { Location = new Point(103, 102), Size = new Size(115, 20), Text = Format(sideLengthMm), TextAlign = HorizontalAlignment.Right };
            hexGroup.Controls.AddRange(new Control[] { radSideLength, radVertexDiameter, radEdgeDiameter, valueLabel, txtHexValue });

            designTabs = new TabControl { Dock = DockStyle.Fill };
            btnAddDesign = new Button { Size = new Size(26, 23), Margin = new Padding(2, 2, 0, 0), Text = "+" };
            btnImportDesign = new Button { AutoSize = true, Margin = new Padding(2, 2, 0, 0), Text = "Import..." };
            FlowLayoutPanel designTabButtons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = Padding.Empty };
            designTabButtons.Controls.AddRange(new Control[] { btnAddDesign, btnImportDesign });
            Panel designTabHeader = new Panel { Dock = DockStyle.Top, Height = 30 };
            // The Fill control is added first so the Right panel docks before it.
            designTabHeader.Controls.Add(designTabs);
            designTabHeader.Controls.Add(designTabButtons);

            btnExportDesign = new Button { AutoSize = true, Text = "Export..." };
            btnDeleteDesign = new Button { AutoSize = true, Text = "Delete" };
            FlowLayoutPanel designToolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 31, WrapContents = false };
            designToolbar.Controls.AddRange(new Control[] { btnExportDesign, btnDeleteDesign });

            Panel designNameRow = new Panel { Dock = DockStyle.Top, Height = 28, Width = 240 };
            Label designNameLabel = new Label { AutoSize = true, Location = new Point(3, 6), Text = "Name:" };
            txtDesignName = new TextBox { Location = new Point(48, 3), Size = new Size(189, 20), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            designNameRow.Controls.AddRange(new Control[] { designNameLabel, txtDesignName });
            faceTextToolTip.SetToolTip(txtDesignName, "Leave empty to use the numbered name");
            faceTextToolTip.SetToolTip(btnAddDesign, "Add a hexagon (copy of the selected one)");
            faceTextToolTip.SetToolTip(btnImportDesign, "Import a single hexagon design as a new hexagon");
            faceTextToolTip.SetToolTip(btnExportDesign, "Export the selected hexagon design");
            faceTextToolTip.SetToolTip(designTabs, "Drag a tab to change the order");

            GroupBox faceGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 110,
                Text = "Lateral faces"
            };
            Label heightLabel = new Label { AutoSize = true, Location = new Point(12, 27), Text = "Face height (mm):" };
            txtFaceHeight = new TextBox { Location = new Point(126, 23), Size = new Size(92, 20), Text = Format(lateralFaceHeightMm), TextAlign = HorizontalAlignment.Right };
            lblTolerance = new Label { AutoSize = true, Location = new Point(12, 53), Text = "Tolerance (mm):" };
            txtFaceTolerance = new TextBox { Location = new Point(126, 49), Size = new Size(92, 20), Text = Format(faceToleranceMm), TextAlign = HorizontalAlignment.Right };
            chkConnectFaces = new CheckBox { AutoSize = true, Location = new Point(12, 77), Text = "Connect faces" };
            faceGroup.Controls.AddRange(new Control[] { heightLabel, txtFaceHeight, lblTolerance, txtFaceTolerance, chkConnectFaces });

            GroupBox orientationGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 72,
                Text = "Hexagon orientation"
            };
            radStandsOnVertex = new RadioButton { AutoSize = true, Location = new Point(12, 22), Text = "Stands on a vertex", Checked = true };
            radStandsOnEdge = new RadioButton { AutoSize = true, Location = new Point(12, 45), Text = "Stands on an edge" };
            orientationGroup.Controls.AddRange(new Control[] { radStandsOnVertex, radStandsOnEdge });

            GroupBox headerGroup = CreateTextGroup("Header", out txtHeader, out cmbHeaderFont, out cmbHeaderFontSize, out btnHeaderTextColor, out pnlHeaderTextColor, 18, true);
            GroupBox subtextGroup = CreateTextGroup("Text", out txtSubtext, out cmbSubtextFont, out cmbSubtextFontSize, out btnSubtextTextColor, out pnlSubtextTextColor, 10, true);

            GroupBox countGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 55,
                Text = "Number of nets"
            };
            numHexagonCount = new NumericUpDown { Location = new Point(12, 21), Size = new Size(57, 20), Minimum = 1, Maximum = 1, Value = 1 };
            countGroup.Controls.Add(numHexagonCount);

            Label generalSettingsLabel = new Label { AutoSize = true, Dock = DockStyle.Top, Font = new Font(Font, FontStyle.Bold), Height = 20, Text = "General settings" };
            Label designSettingsLabel = new Label { AutoSize = true, Dock = DockStyle.Top, Font = new Font(Font, FontStyle.Bold), Height = 30, Padding = new Padding(0, 10, 0, 0), Text = "Hexagon design" };

            GroupBox imageGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 138,
                Text = "Hexagon image"
            };
            btnAddImage = new Button { Location = new Point(12, 21), Size = new Size(100, 23), Text = "Add image..." };
            btnRemoveImage = new Button { Location = new Point(119, 21), Size = new Size(100, 23), Text = "Remove", Enabled = false };
            lblImageName = new Label { AutoEllipsis = true, Location = new Point(12, 53), Size = new Size(218, 20), Text = "PNG, JPEG, BMP, or GIF" };
            Label hexagonImageScaleLabel = new Label { AutoSize = true, Location = new Point(12, 79), Text = "Scale (%):" };
            numHexagonImageScale = CreateImageScaleInput(new Point(78, 75));
            Label hexagonPositionLabel = new Label { AutoSize = true, Location = new Point(12, 108), Text = "Move (1 mm):" };
            imageGroup.Controls.AddRange(new Control[]
            {
                btnAddImage, btnRemoveImage, lblImageName, hexagonImageScaleLabel, numHexagonImageScale, hexagonPositionLabel,
                CreateImageMoveButton(new Point(82, 103), "←", ImageTarget.Hexagon, -1, 0),
                CreateImageMoveButton(new Point(107, 103), "↑", ImageTarget.Hexagon, 0, -1),
                CreateImageMoveButton(new Point(132, 103), "↓", ImageTarget.Hexagon, 0, 1),
                CreateImageMoveButton(new Point(157, 103), "→", ImageTarget.Hexagon, 1, 0)
            });

            GroupBox faceImageGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 138,
                Text = "Face image"
            };
            btnAddFaceImage = new Button { Location = new Point(12, 21), Size = new Size(100, 23), Text = "Add image..." };
            btnRemoveFaceImage = new Button { Location = new Point(119, 21), Size = new Size(100, 23), Text = "Remove", Enabled = false };
            lblFaceImageName = new Label { AutoEllipsis = true, Location = new Point(12, 53), Size = new Size(218, 20), Text = "Repeated and rotated on all six faces" };
            Label faceImageScaleLabel = new Label { AutoSize = true, Location = new Point(12, 79), Text = "Scale (%):" };
            numFaceImageScale = CreateImageScaleInput(new Point(78, 75));
            chkFaceImageFlip = new CheckBox { AutoSize = true, Location = new Point(154, 77), Text = "Flip (180°)" };
            Label facePositionLabel = new Label { AutoSize = true, Location = new Point(12, 108), Text = "Move (1 mm):" };
            faceImageGroup.Controls.AddRange(new Control[]
            {
                btnAddFaceImage, btnRemoveFaceImage, lblFaceImageName, faceImageScaleLabel, numFaceImageScale, chkFaceImageFlip, facePositionLabel,
                CreateImageMoveButton(new Point(82, 103), "←", ImageTarget.Face, -1, 0),
                CreateImageMoveButton(new Point(107, 103), "↑", ImageTarget.Face, 0, -1),
                CreateImageMoveButton(new Point(132, 103), "↓", ImageTarget.Face, 0, 1),
                CreateImageMoveButton(new Point(157, 103), "→", ImageTarget.Face, 1, 0)
            });

            GroupBox faceTextGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 292,
                Text = "Face text"
            };
            txtFaceText = new TextBox
            {
                Location = new Point(12, 21),
                Size = new Size(206, 60),
                Multiline = true,
                WordWrap = false,
                ScrollBars = ScrollBars.Both
            };
            Label faceTextHelp = CreateHelpIcon(StyledTextHelp +
                "\r\n\r\nLine per face example:\r\n" +
                "[size=18][b]Face 1[/b][/size]\r\n" +
                "[size=12]Face 2[/size]");
            Label faceTextFontLabel = new Label { AutoSize = true, Location = new Point(12, 92), Text = "Font:" };
            cmbFaceTextFont = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(48, 89), Size = new Size(116, 21) };
            Label faceTextSizeLabel = new Label { AutoSize = true, Location = new Point(169, 92), Text = "Size:" };
            cmbFaceTextFontSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(202, 89), Size = new Size(43, 21) };
            btnFaceTextColor = new Button { Location = new Point(12, 118), Size = new Size(100, 23), Text = "Text color..." };
            pnlFaceTextColor = new Panel { Location = new Point(119, 119), Size = new Size(28, 21), BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle };
            Label faceTextAlignmentLabel = new Label { AutoSize = true, Location = new Point(12, 153), Text = "Alignment:" };
            cmbFaceTextAlignment = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(80, 150), Size = new Size(84, 21) };
            cmbFaceTextAlignment.Items.AddRange(new object[] { "Center", "Left", "Right" });
            cmbFaceTextAlignment.SelectedIndex = 0;
            chkFaceTextFlipTopToBottom = new CheckBox { Appearance = Appearance.Button, Location = new Point(12, 175), Size = new Size(152, 23), Text = "Rotate 180°", TextAlign = ContentAlignment.MiddleCenter, Checked = true };
            Button btnInsertFaceTextImage = CreateInsertImageButton(new Point(170, 175), txtFaceText);
            radFaceTextRepeat = new RadioButton { AutoSize = true, Location = new Point(12, 205), Text = "Repeat on every face", Checked = true };
            radFaceTextWrapAround = new RadioButton { AutoSize = true, Location = new Point(12, 228), Text = "Wrap around" };
            radFaceTextLinePerFace = new RadioButton { AutoSize = true, Location = new Point(12, 251), Text = "Line per face" };
            Label faceTextStartLabel = new Label { AutoSize = true, Location = new Point(136, 253), Text = "Start at:" };
            cmbFaceTextStartAt = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(192, 249), Size = new Size(45, 21), Enabled = false };
            for (int faceNumber = 1; faceNumber <= 6; faceNumber++)
            {
                cmbFaceTextStartAt.Items.Add(faceNumber.ToString(CultureInfo.InvariantCulture));
            }
            cmbFaceTextStartAt.SelectedIndex = 0;
            PopulateFontOptions(cmbFaceTextFont, cmbFaceTextFontSize, 10);
            faceTextGroup.Controls.AddRange(new Control[]
            {
                faceTextHelp, txtFaceText, faceTextFontLabel, cmbFaceTextFont, faceTextSizeLabel, cmbFaceTextFontSize,
                btnFaceTextColor, pnlFaceTextColor, faceTextAlignmentLabel, cmbFaceTextAlignment,
                chkFaceTextFlipTopToBottom, btnInsertFaceTextImage,
                radFaceTextRepeat, radFaceTextWrapAround, radFaceTextLinePerFace, faceTextStartLabel, cmbFaceTextStartAt
            });

            GroupBox backgroundColorGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 80,
                Text = "Background colors"
            };
            btnHexagonBackgroundColor = new Button { Location = new Point(12, 20), Size = new Size(100, 23), Text = "Hexagon..." };
            pnlHexagonBackgroundColor = new Panel { Location = new Point(119, 21), Size = new Size(28, 21), BackColor = hexagonBackgroundColor, BorderStyle = BorderStyle.FixedSingle };
            btnFaceBackgroundColor = new Button { Location = new Point(12, 49), Size = new Size(100, 23), Text = "Faces..." };
            pnlFaceBackgroundColor = new Panel { Location = new Point(119, 50), Size = new Size(28, 21), BackColor = faceBackgroundColor, BorderStyle = BorderStyle.FixedSingle };
            backgroundColorGroup.Controls.AddRange(new Control[] { btnHexagonBackgroundColor, pnlHexagonBackgroundColor, btnFaceBackgroundColor, pnlFaceBackgroundColor });

            bool wideLayoutActive = false;
            Action arrangeBackgroundColors = () =>
            {
                if (wideLayoutActive)
                {
                    int facesColumnLeft = Math.Max(135, backgroundColorGroup.ClientSize.Width / 2);
                    btnHexagonBackgroundColor.Location = new Point(12, 20);
                    pnlHexagonBackgroundColor.Location = new Point(119, 21);
                    btnFaceBackgroundColor.Location = new Point(facesColumnLeft, 20);
                    pnlFaceBackgroundColor.Location = new Point(facesColumnLeft + 107, 21);
                }
                else
                {
                    btnHexagonBackgroundColor.Location = new Point(12, 20);
                    pnlHexagonBackgroundColor.Location = new Point(119, 21);
                    btnFaceBackgroundColor.Location = new Point(12, 49);
                    pnlFaceBackgroundColor.Location = new Point(119, 50);
                }
            };
            backgroundColorGroup.Resize += delegate { arrangeBackgroundColors(); };

            GroupBox lineStyleGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 82,
                Text = "Line style"
            };
            btnLineColor = new Button { Location = new Point(12, 21), Size = new Size(100, 23), Text = "Line color..." };
            pnlLineColor = new Panel { Location = new Point(119, 22), Size = new Size(28, 21), BackColor = lineColor, BorderStyle = BorderStyle.FixedSingle };
            Label thicknessLabel = new Label { AutoSize = true, Location = new Point(12, 53), Text = "Thickness (mm):" };
            numLineThickness = new NumericUpDown
            {
                Location = new Point(105, 49),
                Size = new Size(69, 20),
                DecimalPlaces = 2,
                Increment = 0.05m,
                Minimum = 0m,
                Maximum = 10m,
                Value = 0.35m
            };
            faceTextToolTip.SetToolTip(numLineThickness, "0 draws no lines");
            lineStyleGroup.Controls.AddRange(new Control[] { btnLineColor, pnlLineColor, thicknessLabel, numLineThickness });
            Action arrangeLineStyle = () =>
            {
                if (wideLayoutActive)
                {
                    thicknessLabel.Location = new Point(170, 25);
                    numLineThickness.Location = new Point(263, 21);
                }
                else
                {
                    thicknessLabel.Location = new Point(12, 53);
                    numLineThickness.Location = new Point(105, 49);
                }
            };

            lblConversions = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 72,
                Padding = new Padding(3, 8, 3, 3),
                BorderStyle = BorderStyle.FixedSingle
            };

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 39,
                FlowDirection = FlowDirection.LeftToRight
            };
            btnCreate = new Button { AutoSize = true, Text = "Create" };
            btnPrint = new Button { AutoSize = true, Text = "Print...", Enabled = false };
            btnLoadLayout = new Button { AutoSize = true, Text = "Load layout..." };
            btnSaveLayout = new Button { AutoSize = true, Text = "Save layout..." };
            buttons.Controls.AddRange(new Control[] { btnCreate, btnPrint });
            FlowLayoutPanel layoutButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 33,
                FlowDirection = FlowDirection.LeftToRight
            };
            layoutButtons.Controls.AddRange(new Control[] { btnLoadLayout, btnSaveLayout });

            TableLayoutPanel generalSettingsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            TableLayoutPanel hexagonDesignLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            Action<bool> configureResponsiveLayout = wideLayout =>
            {
                wideLayoutActive = wideLayout;
                generalSettingsLayout.SuspendLayout();
                generalSettingsLayout.Controls.Clear();
                generalSettingsLayout.ColumnStyles.Clear();
                generalSettingsLayout.RowStyles.Clear();
                generalSettingsLayout.ColumnCount = wideLayout ? 2 : 1;
                generalSettingsLayout.RowCount = wideLayout ? 2 : 4;
                for (int column = 0; column < generalSettingsLayout.ColumnCount; column++)
                {
                    generalSettingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / generalSettingsLayout.ColumnCount));
                }
                if (wideLayout)
                {
                    generalSettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
                    generalSettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
                    generalSettingsLayout.Height = 227;
                    generalSettingsLayout.Controls.Add(hexGroup, 0, 0);
                    generalSettingsLayout.Controls.Add(faceGroup, 1, 0);
                    generalSettingsLayout.Controls.Add(orientationGroup, 0, 1);
                    generalSettingsLayout.Controls.Add(countGroup, 1, 1);
                }
                else
                {
                    generalSettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
                    generalSettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
                    generalSettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
                    generalSettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
                    generalSettingsLayout.Height = 392;
                    generalSettingsLayout.Controls.Add(hexGroup, 0, 0);
                    generalSettingsLayout.Controls.Add(faceGroup, 0, 1);
                    generalSettingsLayout.Controls.Add(orientationGroup, 0, 2);
                    generalSettingsLayout.Controls.Add(countGroup, 0, 3);
                }
                generalSettingsLayout.ResumeLayout();

                hexagonDesignLayout.SuspendLayout();
                hexagonDesignLayout.Controls.Clear();
                hexagonDesignLayout.ColumnStyles.Clear();
                hexagonDesignLayout.RowStyles.Clear();
                hexagonDesignLayout.ColumnCount = wideLayout ? 2 : 1;
                hexagonDesignLayout.RowCount = wideLayout ? 5 : 7;
                for (int column = 0; column < hexagonDesignLayout.ColumnCount; column++)
                {
                    hexagonDesignLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / hexagonDesignLayout.ColumnCount));
                }
                if (wideLayout)
                {
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
                    hexagonDesignLayout.Height = 577;
                    hexagonDesignLayout.Controls.Add(lineStyleGroup, 0, 0);
                    hexagonDesignLayout.SetColumnSpan(lineStyleGroup, 2);
                    hexagonDesignLayout.Controls.Add(headerGroup, 0, 1);
                    hexagonDesignLayout.Controls.Add(subtextGroup, 0, 2);
                    hexagonDesignLayout.Controls.Add(faceTextGroup, 1, 1);
                    hexagonDesignLayout.SetRowSpan(faceTextGroup, 2);
                    hexagonDesignLayout.Controls.Add(backgroundColorGroup, 0, 3);
                    hexagonDesignLayout.SetColumnSpan(backgroundColorGroup, 2);
                    hexagonDesignLayout.Controls.Add(imageGroup, 0, 4);
                    hexagonDesignLayout.Controls.Add(faceImageGroup, 1, 4);
                }
                else
                {
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 280));
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
                    hexagonDesignLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
                    hexagonDesignLayout.Height = 1022;
                    hexagonDesignLayout.Controls.Add(lineStyleGroup, 0, 0);
                    hexagonDesignLayout.Controls.Add(headerGroup, 0, 1);
                    hexagonDesignLayout.Controls.Add(subtextGroup, 0, 2);
                    hexagonDesignLayout.Controls.Add(faceTextGroup, 0, 3);
                    hexagonDesignLayout.Controls.Add(backgroundColorGroup, 0, 4);
                    hexagonDesignLayout.Controls.Add(imageGroup, 0, 5);
                    hexagonDesignLayout.Controls.Add(faceImageGroup, 0, 6);
                }
                hexagonDesignLayout.ResumeLayout();

                foreach (Control group in new Control[]
                {
                    hexGroup, faceGroup, orientationGroup, lineStyleGroup, countGroup,
                    headerGroup, subtextGroup, faceTextGroup, backgroundColorGroup, imageGroup, faceImageGroup
                })
                {
                    group.Dock = DockStyle.Fill;
                    group.Margin = Padding.Empty;
                }
                arrangeBackgroundColors();
                arrangeLineStyle();
                settingsPanel.Width = wideLayout ? Math.Min(640, Math.Max(600, ClientSize.Width / 3)) : 310;
            };

            configureResponsiveLayout(false);

            // Docked controls are added in reverse display order.
            settingsPanel.Controls.Add(lblConversions);
            settingsPanel.Controls.Add(hexagonDesignLayout);
            settingsPanel.Controls.Add(designToolbar);
            settingsPanel.Controls.Add(designNameRow);
            settingsPanel.Controls.Add(designTabHeader);
            settingsPanel.Controls.Add(designSettingsLabel);
            settingsPanel.Controls.Add(generalSettingsLayout);
            settingsPanel.Controls.Add(generalSettingsLabel);
            settingsPanel.Controls.Add(buttons);
            settingsPanel.Controls.Add(layoutButtons);
            settingsPanel.Controls.Add(instructions);

            preview = new NetPreviewPanel
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.ControlDark,
                BorderStyle = BorderStyle.FixedSingle
            };

            Controls.Add(preview);
            Controls.Add(settingsPanel);

            printDocument = new PrintDocument();
            printDocument.DocumentName = "Hexagonal prism net";
            printDocument.OriginAtMargins = true;
            printDocument.PrintPage += printDocument_PrintPage;

            radSideLength.CheckedChanged += measurementTypeChanged;
            radVertexDiameter.CheckedChanged += measurementTypeChanged;
            radEdgeDiameter.CheckedChanged += measurementTypeChanged;
            txtHexValue.TextChanged += inputChanged;
            txtFaceHeight.TextChanged += inputChanged;
            txtFaceTolerance.TextChanged += inputChanged;
            chkConnectFaces.CheckedChanged += lineStyleChanged;
            numHexagonCount.ValueChanged += numHexagonCount_ValueChanged;
            designTabs.SelectedIndexChanged += designTabs_SelectedIndexChanged;
            designTabs.MouseDown += designTabs_MouseDown;
            designTabs.MouseMove += designTabs_MouseMove;
            designTabs.MouseUp += designTabs_MouseUp;
            btnAddDesign.Click += btnAddDesign_Click;
            btnImportDesign.Click += btnImportDesign_Click;
            btnExportDesign.Click += btnExportDesign_Click;
            btnDeleteDesign.Click += btnDeleteDesign_Click;
            txtDesignName.TextChanged += txtDesignName_TextChanged;
            radStandsOnVertex.CheckedChanged += orientationChanged;
            radStandsOnEdge.CheckedChanged += orientationChanged;
            txtHeader.TextChanged += textAppearanceChanged;
            cmbHeaderFont.SelectedIndexChanged += textAppearanceChanged;
            cmbHeaderFontSize.SelectedIndexChanged += textAppearanceChanged;
            btnHeaderTextColor.Click += btnTextColor_Click;
            txtSubtext.TextChanged += textAppearanceChanged;
            cmbSubtextFont.SelectedIndexChanged += textAppearanceChanged;
            cmbSubtextFontSize.SelectedIndexChanged += textAppearanceChanged;
            btnSubtextTextColor.Click += btnTextColor_Click;
            btnAddImage.Click += btnAddImage_Click;
            btnRemoveImage.Click += btnRemoveImage_Click;
            numHexagonImageScale.ValueChanged += imageAppearanceChanged;
            btnAddFaceImage.Click += btnAddFaceImage_Click;
            btnRemoveFaceImage.Click += btnRemoveFaceImage_Click;
            numFaceImageScale.ValueChanged += imageAppearanceChanged;
            chkFaceImageFlip.CheckedChanged += imageAppearanceChanged;
            txtFaceText.TextChanged += faceTextAppearanceChanged;
            cmbFaceTextFont.SelectedIndexChanged += faceTextAppearanceChanged;
            cmbFaceTextFontSize.SelectedIndexChanged += faceTextAppearanceChanged;
            btnFaceTextColor.Click += btnTextColor_Click;
            cmbFaceTextAlignment.SelectedIndexChanged += faceTextAppearanceChanged;
            chkFaceTextFlipTopToBottom.CheckedChanged += faceTextAppearanceChanged;
            radFaceTextRepeat.CheckedChanged += faceTextAppearanceChanged;
            radFaceTextWrapAround.CheckedChanged += faceTextAppearanceChanged;
            radFaceTextLinePerFace.CheckedChanged += faceTextAppearanceChanged;
            cmbFaceTextStartAt.SelectedIndexChanged += faceTextAppearanceChanged;
            btnHexagonBackgroundColor.Click += btnBackgroundColor_Click;
            btnFaceBackgroundColor.Click += btnBackgroundColor_Click;
            btnLineColor.Click += btnLineColor_Click;
            numLineThickness.ValueChanged += lineStyleChanged;
            btnLoadLayout.Click += btnLoadLayout_Click;
            btnSaveLayout.Click += btnSaveLayout_Click;
            btnCreate.Click += btnCreate_Click;
            btnPrint.Click += btnPrint_Click;
            preview.Paint += preview_Paint;
            Resize += delegate
            {
                configureResponsiveLayout(ClientSize.Width >= 1180);
            };

            designs.Add(ReadDesignFromEditor());
            RebuildDesignTabs();
            UpdateFaceOptions();
            UpdateFaceTextOptions();
            UpdateMaximumNetCount();
            UpdateConversionText();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                faceTextToolTip.Dispose();
                foreach (HexagonDesign design in designs)
                {
                    design.Dispose();
                }
                foreach (Image image in textImages.Values)
                {
                    image.Dispose();
                }
                textImages.Clear();
            }
            base.Dispose(disposing);
        }

        private void numHexagonCount_ValueChanged(object sender, EventArgs e)
        {
            if (loadingLayout)
            {
                return;
            }

            SaveActiveDesign();
            int requestedCount = (int)numHexagonCount.Value;
            while (designs.Count < requestedCount)
            {
                HexagonDesign copy = designs[activeDesignIndex].Clone();
                if (!String.IsNullOrWhiteSpace(copy.Name))
                {
                    copy.Name += " (copy)";
                }
                designs.Add(copy);
            }
            while (designs.Count > requestedCount)
            {
                HexagonDesign removedDesign = designs[designs.Count - 1];
                designs.RemoveAt(designs.Count - 1);
                removedDesign.Dispose();
            }

            activeDesignIndex = Math.Min(activeDesignIndex, designs.Count - 1);
            RebuildDesignTabs();
            LoadActiveDesign();
            UpdateA4Layout();
            preview.Invalidate();
        }

        private void designTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (loadingDesign || designTabs.SelectedIndex < 0 || designTabs.SelectedIndex == activeDesignIndex)
            {
                return;
            }

            SaveActiveDesign();
            activeDesignIndex = designTabs.SelectedIndex;
            LoadActiveDesign();
            preview.Invalidate();
        }

        private void RebuildDesignTabs()
        {
            loadingDesign = true;
            designTabs.TabPages.Clear();
            for (int i = 0; i < designs.Count; i++)
            {
                designTabs.TabPages.Add(GetDesignTitle(i));
            }
            designTabs.SelectedIndex = activeDesignIndex;
            loadingDesign = false;
            UpdateDesignButtons();
        }

        private string GetDesignTitle(int index)
        {
            string name = designs[index].Name;
            return String.IsNullOrWhiteSpace(name) ? "Hexagon " + (index + 1) : name.Trim();
        }

        private void UpdateDesignTabTitles()
        {
            for (int i = 0; i < designTabs.TabCount && i < designs.Count; i++)
            {
                string title = GetDesignTitle(i);
                if (designTabs.TabPages[i].Text != title)
                {
                    designTabs.TabPages[i].Text = title;
                }
            }
        }

        private void txtDesignName_TextChanged(object sender, EventArgs e)
        {
            if (loadingDesign)
            {
                return;
            }

            SaveActiveDesign();
            UpdateDesignTabTitles();
        }

        private void UpdateDesignButtons()
        {
            bool canAdd = designs.Count < (int)numHexagonCount.Maximum;
            btnAddDesign.Enabled = canAdd;
            btnImportDesign.Enabled = canAdd;
            btnDeleteDesign.Enabled = designs.Count > 1;
        }

        private void btnAddDesign_Click(object sender, EventArgs e)
        {
            if (numHexagonCount.Value >= numHexagonCount.Maximum)
            {
                return;
            }

            // The count handler appends a copy of the active design.
            numHexagonCount.Value++;
            designTabs.SelectedIndex = designs.Count - 1;
        }

        private void btnDeleteDesign_Click(object sender, EventArgs e)
        {
            if (designs.Count <= 1)
            {
                return;
            }

            HexagonDesign removedDesign = designs[activeDesignIndex];
            designs.RemoveAt(activeDesignIndex);
            removedDesign.Dispose();
            activeDesignIndex = Math.Min(activeDesignIndex, designs.Count - 1);
            SetDesignCount();
            RebuildDesignTabs();
            LoadActiveDesign();
            UpdateA4Layout();
            preview.Invalidate();
        }

        // Updates the count control without letting its handler add or remove designs.
        private void SetDesignCount()
        {
            bool wasLoadingLayout = loadingLayout;
            loadingLayout = true;
            try
            {
                numHexagonCount.Value = designs.Count;
            }
            finally
            {
                loadingLayout = wasLoadingLayout;
            }
        }

        private void btnExportDesign_Click(object sender, EventArgs e)
        {
            SaveActiveDesign();
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = DesignFileFilter;
                dialog.DefaultExt = "hexagon";
                dialog.AddExtension = true;
                dialog.FileName = String.Join("_", GetDesignTitle(activeDesignIndex).Split(Path.GetInvalidFileNameChars()));
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(NetDesignFile));
                    using (FileStream stream = File.Create(dialog.FileName))
                    {
                        serializer.WriteObject(stream, CreateDesignFile(designs[activeDesignIndex]));
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, "The hexagon could not be exported.\r\n\r\n" + exception.Message, "Export hexagon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnImportDesign_Click(object sender, EventArgs e)
        {
            if (designs.Count >= (int)numHexagonCount.Maximum)
            {
                MessageBox.Show(this, "No more hexagons fit on one A4 page with the current measurements.", "Import hexagon", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = DesignFileFilter;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                HexagonDesign importedDesign;
                try
                {
                    NetDesignFile savedDesign;
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(NetDesignFile));
                    using (FileStream stream = File.OpenRead(dialog.FileName))
                    {
                        savedDesign = (NetDesignFile)serializer.ReadObject(stream);
                    }
                    NetLayoutFile defaults = new NetLayoutFile { LineColorArgb = Color.Gray.ToArgb(), LineThickness = 0.35m };
                    importedDesign = CreateDesignFromFile(savedDesign, defaults, Path.GetDirectoryName(dialog.FileName));
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, "The hexagon could not be imported.\r\n\r\n" + exception.Message, "Import hexagon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                SaveActiveDesign();
                designs.Add(importedDesign);
                activeDesignIndex = designs.Count - 1;
                SetDesignCount();
                RebuildDesignTabs();
                LoadActiveDesign();
                UpdateA4Layout();
                preview.Invalidate();
            }
        }

        private int GetTabIndexAt(Point location)
        {
            for (int i = 0; i < designTabs.TabCount; i++)
            {
                if (designTabs.GetTabRect(i).Contains(location))
                {
                    return i;
                }
            }
            return -1;
        }

        private void designTabs_MouseDown(object sender, MouseEventArgs e)
        {
            draggedTabIndex = e.Button == MouseButtons.Left ? GetTabIndexAt(e.Location) : -1;
        }

        private void designTabs_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedTabIndex < 0 || e.Button != MouseButtons.Left)
            {
                return;
            }

            int targetIndex = GetTabIndexAt(e.Location);
            if (targetIndex < 0 || targetIndex == draggedTabIndex)
            {
                return;
            }

            // Only move once the pointer has crossed the target tab's midpoint,
            // so tabs of different widths do not swap back and forth.
            Rectangle targetRect = designTabs.GetTabRect(targetIndex);
            int midpoint = targetRect.Left + targetRect.Width / 2;
            if ((targetIndex > draggedTabIndex && e.X < midpoint) || (targetIndex < draggedTabIndex && e.X > midpoint))
            {
                return;
            }

            SaveActiveDesign();
            HexagonDesign activeDesign = designs[activeDesignIndex];
            HexagonDesign movedDesign = designs[draggedTabIndex];
            designs.RemoveAt(draggedTabIndex);
            designs.Insert(targetIndex, movedDesign);
            activeDesignIndex = designs.IndexOf(activeDesign);
            draggedTabIndex = targetIndex;
            loadingDesign = true;
            UpdateDesignTabTitles();
            designTabs.SelectedIndex = activeDesignIndex;
            loadingDesign = false;
            preview.Invalidate();
        }

        private void designTabs_MouseUp(object sender, MouseEventArgs e)
        {
            draggedTabIndex = -1;
        }

        private void SaveActiveDesign()
        {
            if (loadingDesign || designs.Count == 0 || activeDesignIndex < 0 || activeDesignIndex >= designs.Count)
            {
                return;
            }

            HexagonDesign design = designs[activeDesignIndex];
            design.Name = txtDesignName.Text;
            design.LineColor = lineColor;
            design.LineThickness = numLineThickness.Value;
            design.HeaderText = txtHeader.Text;
            design.HeaderFontName = cmbHeaderFont.SelectedItem as string;
            design.HeaderFontSize = GetSelectedFontSize(cmbHeaderFontSize);
            design.HeaderTextColor = headerTextColor;
            design.Subtext = txtSubtext.Text;
            design.SubtextFontName = cmbSubtextFont.SelectedItem as string;
            design.SubtextFontSize = GetSelectedFontSize(cmbSubtextFontSize);
            design.SubtextColor = subtextTextColor;
            design.HexagonBackgroundColor = hexagonBackgroundColor;
            design.FaceBackgroundColor = faceBackgroundColor;
            design.HexagonImage = hexagonImage;
            design.HexagonImageScale = numHexagonImageScale.Value;
            design.FaceImage = faceImage;
            design.FaceImageScale = numFaceImageScale.Value;
            design.FaceImageFlip = chkFaceImageFlip.Checked;
            design.FaceText = txtFaceText.Text;
            design.FaceTextFontName = cmbFaceTextFont.SelectedItem as string;
            design.FaceTextFontSize = GetSelectedFontSize(cmbFaceTextFontSize);
            design.FaceTextColor = pnlFaceTextColor.BackColor;
            design.FaceTextAlignment = cmbFaceTextAlignment.SelectedIndex;
            design.FaceTextFlipTopToBottom = chkFaceTextFlipTopToBottom.Checked;
            design.FaceTextWrapAround = radFaceTextWrapAround.Checked;
            design.FaceTextLinePerFace = radFaceTextLinePerFace.Checked;
            design.FaceTextStartAt = cmbFaceTextStartAt.SelectedIndex + 1;
        }

        private HexagonDesign ReadDesignFromEditor()
        {
            HexagonDesign design = new HexagonDesign();
            design.Name = txtDesignName.Text;
            design.LineColor = lineColor;
            design.LineThickness = numLineThickness.Value;
            design.HeaderText = txtHeader.Text;
            design.HeaderFontName = cmbHeaderFont.SelectedItem as string;
            design.HeaderFontSize = GetSelectedFontSize(cmbHeaderFontSize);
            design.HeaderTextColor = headerTextColor;
            design.Subtext = txtSubtext.Text;
            design.SubtextFontName = cmbSubtextFont.SelectedItem as string;
            design.SubtextFontSize = GetSelectedFontSize(cmbSubtextFontSize);
            design.SubtextColor = subtextTextColor;
            design.HexagonBackgroundColor = hexagonBackgroundColor;
            design.FaceBackgroundColor = faceBackgroundColor;
            design.HexagonImage = hexagonImage;
            design.HexagonImageScale = numHexagonImageScale.Value;
            design.FaceImage = faceImage;
            design.FaceImageScale = numFaceImageScale.Value;
            design.FaceImageFlip = chkFaceImageFlip.Checked;
            design.FaceText = txtFaceText.Text;
            design.FaceTextFontName = cmbFaceTextFont.SelectedItem as string;
            design.FaceTextFontSize = GetSelectedFontSize(cmbFaceTextFontSize);
            design.FaceTextColor = pnlFaceTextColor.BackColor;
            design.FaceTextAlignment = cmbFaceTextAlignment.SelectedIndex;
            design.FaceTextFlipTopToBottom = chkFaceTextFlipTopToBottom.Checked;
            design.FaceTextWrapAround = radFaceTextWrapAround.Checked;
            design.FaceTextLinePerFace = radFaceTextLinePerFace.Checked;
            design.FaceTextStartAt = cmbFaceTextStartAt.SelectedIndex + 1;
            return design;
        }

        private void LoadActiveDesign()
        {
            if (designs.Count == 0)
            {
                return;
            }

            HexagonDesign design = designs[activeDesignIndex];
            loadingDesign = true;
            txtDesignName.Text = design.Name ?? String.Empty;
            lineColor = design.LineColor;
            pnlLineColor.BackColor = lineColor;
            numLineThickness.Value = Math.Max(numLineThickness.Minimum, Math.Min(numLineThickness.Maximum, design.LineThickness));
            txtHeader.Text = design.HeaderText;
            SelectComboItem(cmbHeaderFont, design.HeaderFontName);
            SelectComboItem(cmbHeaderFontSize, design.HeaderFontSize.ToString(CultureInfo.InvariantCulture));
            headerTextColor = design.HeaderTextColor;
            pnlHeaderTextColor.BackColor = headerTextColor;
            txtSubtext.Text = design.Subtext;
            SelectComboItem(cmbSubtextFont, design.SubtextFontName);
            SelectComboItem(cmbSubtextFontSize, design.SubtextFontSize.ToString(CultureInfo.InvariantCulture));
            subtextTextColor = design.SubtextColor;
            pnlSubtextTextColor.BackColor = subtextTextColor;
            hexagonBackgroundColor = design.HexagonBackgroundColor;
            pnlHexagonBackgroundColor.BackColor = hexagonBackgroundColor;
            faceBackgroundColor = design.FaceBackgroundColor;
            pnlFaceBackgroundColor.BackColor = faceBackgroundColor;
            hexagonImage = design.HexagonImage;
            numHexagonImageScale.Value = design.HexagonImageScale;
            lblImageName.Text = GetImageDisplayName(design.HexagonImagePath, hexagonImage, "PNG, JPEG, BMP, or GIF");
            btnRemoveImage.Enabled = hexagonImage != null;
            faceImage = design.FaceImage;
            numFaceImageScale.Value = design.FaceImageScale;
            chkFaceImageFlip.Checked = design.FaceImageFlip;
            lblFaceImageName.Text = GetImageDisplayName(design.FaceImagePath, faceImage, "Repeated and rotated on all six faces");
            btnRemoveFaceImage.Enabled = faceImage != null;
            txtFaceText.Text = design.FaceText;
            SelectComboItem(cmbFaceTextFont, design.FaceTextFontName);
            SelectComboItem(cmbFaceTextFontSize, design.FaceTextFontSize.ToString(CultureInfo.InvariantCulture));
            pnlFaceTextColor.BackColor = design.FaceTextColor;
            cmbFaceTextAlignment.SelectedIndex = Math.Max(0, Math.Min(2, design.FaceTextAlignment));
            chkFaceTextFlipTopToBottom.Checked = design.FaceTextFlipTopToBottom;
            radFaceTextLinePerFace.Checked = design.FaceTextLinePerFace;
            radFaceTextWrapAround.Checked = !design.FaceTextLinePerFace && design.FaceTextWrapAround;
            radFaceTextRepeat.Checked = !design.FaceTextLinePerFace && !design.FaceTextWrapAround;
            cmbFaceTextStartAt.SelectedIndex = Math.Max(0, Math.Min(5, design.FaceTextStartAt - 1));
            UpdateFaceTextOptions();
            loadingDesign = false;
        }

        private static int GetSelectedFontSize(ComboBox comboBox)
        {
            int value;
            return int.TryParse(comboBox.SelectedItem as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 10;
        }

        private static void SelectComboItem(ComboBox comboBox, string value)
        {
            if (value != null)
            {
                comboBox.SelectedItem = value;
            }
            if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private void measurementTypeChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                txtHexValue.Text = Format(GetSelectedMeasurement(sideLengthMm));
            }
        }

        private void inputChanged(object sender, EventArgs e)
        {
            UpdateFaceOptions();
            UpdateMaximumNetCount();
            UpdateConversionText();
        }

        private void UpdateFaceOptions()
        {
            float height;
            bool hasFaces = TryParseNonNegative(txtFaceHeight.Text, out height) && height > 0;
            txtFaceTolerance.Enabled = hasFaces;
            lblTolerance.Enabled = hasFaces;
            chkConnectFaces.Enabled = hasFaces;
        }

        private void orientationChanged(object sender, EventArgs e)
        {
            if (!((RadioButton)sender).Checked)
            {
                return;
            }

            UpdateMaximumNetCount();
            if (!hasNet)
            {
                return;
            }

            bool fitsOnA4 = TryGetA4Orientation(GetNetBounds(sideLengthMm, lateralFaceHeightMm, faceToleranceMm, radStandsOnVertex.Checked), out a4Landscape);
            btnPrint.Enabled = fitsOnA4;
            if (fitsOnA4)
            {
                ConfigureA4Page();
            }
            preview.Invalidate();
        }

        private void textAppearanceChanged(object sender, EventArgs e)
        {
            SaveActiveDesign();
            if (hasNet)
            {
                preview.Invalidate();
            }
        }

        private void faceTextAppearanceChanged(object sender, EventArgs e)
        {
            UpdateFaceTextOptions();
            SaveActiveDesign();
            if (hasNet)
            {
                preview.Invalidate();
            }
        }

        private void UpdateFaceTextOptions()
        {
            cmbFaceTextStartAt.Enabled = radFaceTextWrapAround.Checked || radFaceTextLinePerFace.Checked;
        }

        private const string StyledTextHelp =
            "Use BBCode-style tags for inline formatting.\r\n" +
            "[b]bold[/b], [i]italic[/i], [u]underlined[/u]\r\n" +
            "[size=18]large[/size], [font=Arial]font[/font], [color=#C00000]red[/color]\r\n\r\n" +
            "Insert images with HTML. By default an image is as high as the text:\r\n" +
            "<img src=\"C:\\Pictures\\star.png\">\r\n" +
            "<img src=\"star.png\" height=\"8mm\">  (height/width in pt or mm)";

        // A small "?" for the top-right corner of a GroupBox. The group is
        // 200 wide until it is docked; Anchor keeps the icon in the corner.
        private Label CreateHelpIcon(string helpText)
        {
            Label help = new Label
            {
                AutoSize = false,
                Location = new Point(200 - 22, 0),
                Size = new Size(16, 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = "?",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = SystemColors.HotTrack,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Help
            };
            faceTextToolTip.SetToolTip(help, helpText);
            help.Click += delegate
            {
                faceTextToolTip.Show(helpText, help, 0, help.Height, 30000);
            };
            return help;
        }

        private Button CreateInsertImageButton(Point location, TextBox target)
        {
            Button button = new Button { Location = location, Size = new Size(75, 23), Text = "Image..." };
            faceTextToolTip.SetToolTip(button, "Insert an <img> tag at the cursor");
            button.Click += delegate { InsertImageTag(target); };
            return button;
        }

        private void InsertImageTag(TextBox target)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string tag = "<img src=\"" + dialog.FileName + "\">";
                int selectionStart = target.SelectionStart;
                target.SelectedText = tag;
                target.SelectionStart = selectionStart + tag.Length;
                target.Focus();
            }
        }

        private void btnLineColor_Click(object sender, EventArgs e)
        {
            ShowColorPickerMenu((Control)sender, lineColor, delegate(Color selectedColor)
            {
                lineColor = selectedColor;
                pnlLineColor.BackColor = lineColor;
                lineStyleChanged(sender, e);
            });
        }

        private void lineStyleChanged(object sender, EventArgs e)
        {
            SaveActiveDesign();
            if (hasNet)
            {
                preview.Invalidate();
            }
        }

        private void btnTextColor_Click(object sender, EventArgs e)
        {
            bool isHeader = sender == btnHeaderTextColor;
            bool isFaceText = sender == btnFaceTextColor;
            Color currentColor = isHeader ? headerTextColor : (isFaceText ? pnlFaceTextColor.BackColor : subtextTextColor);
            ShowColorPickerMenu((Control)sender, currentColor, delegate(Color selectedColor)
            {
                if (isHeader)
                {
                    headerTextColor = selectedColor;
                    pnlHeaderTextColor.BackColor = headerTextColor;
                }
                else if (isFaceText)
                {
                    pnlFaceTextColor.BackColor = selectedColor;
                }
                else
                {
                    subtextTextColor = selectedColor;
                    pnlSubtextTextColor.BackColor = subtextTextColor;
                }
                if (isFaceText)
                {
                    faceTextAppearanceChanged(sender, e);
                }
                else
                {
                    textAppearanceChanged(sender, e);
                }
            });
        }

        private void btnAddImage_Click(object sender, EventArgs e)
        {
            Image loadedImage;
            string fileName;
            if (!TrySelectImage(out loadedImage, out fileName))
            {
                return;
            }

            DisposeImage(ref hexagonImage);
            hexagonImage = loadedImage;
            designs[activeDesignIndex].HexagonImagePath = fileName;
            designs[activeDesignIndex].HexagonImageOffsetX = 0;
            designs[activeDesignIndex].HexagonImageOffsetY = 0;
            lblImageName.Text = Path.GetFileName(fileName);
            btnRemoveImage.Enabled = true;
            imageAppearanceChanged(sender, e);
        }

        private void btnAddFaceImage_Click(object sender, EventArgs e)
        {
            Image loadedImage;
            string fileName;
            if (!TrySelectImage(out loadedImage, out fileName))
            {
                return;
            }

            DisposeImage(ref faceImage);
            faceImage = loadedImage;
            designs[activeDesignIndex].FaceImagePath = fileName;
            designs[activeDesignIndex].FaceImageOffsetX = 0;
            designs[activeDesignIndex].FaceImageOffsetY = 0;
            lblFaceImageName.Text = Path.GetFileName(fileName);
            btnRemoveFaceImage.Enabled = true;
            imageAppearanceChanged(sender, e);
        }

        private void btnRemoveImage_Click(object sender, EventArgs e)
        {
            DisposeImage(ref hexagonImage);
            designs[activeDesignIndex].HexagonImagePath = null;
            designs[activeDesignIndex].HexagonImageOffsetX = 0;
            designs[activeDesignIndex].HexagonImageOffsetY = 0;
            lblImageName.Text = "PNG, JPEG, BMP, or GIF";
            btnRemoveImage.Enabled = false;
            imageAppearanceChanged(sender, e);
        }

        private void btnRemoveFaceImage_Click(object sender, EventArgs e)
        {
            DisposeImage(ref faceImage);
            designs[activeDesignIndex].FaceImagePath = null;
            designs[activeDesignIndex].FaceImageOffsetX = 0;
            designs[activeDesignIndex].FaceImageOffsetY = 0;
            lblFaceImageName.Text = "Repeated and rotated on all six faces";
            btnRemoveFaceImage.Enabled = false;
            imageAppearanceChanged(sender, e);
        }

        private bool TrySelectImage(out Image image, out string fileName)
        {
            image = null;
            fileName = null;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select clip art or image";
                dialog.Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|PNG files|*.png|JPEG files|*.jpg;*.jpeg|All files|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                try
                {
                    using (Image source = Image.FromFile(dialog.FileName))
                    {
                        image = (Image)source.Clone();
                    }
                    fileName = System.IO.Path.GetFullPath(dialog.FileName);
                    return true;
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, "The selected file could not be loaded as an image.\r\n\r\n" + exception.Message, "Unable to load image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }
        }

        private static void DisposeImage(ref Image image)
        {
            if (image != null)
            {
                image.Dispose();
                image = null;
            }
        }

        private void imageAppearanceChanged(object sender, EventArgs e)
        {
            SaveActiveDesign();
            if (hasNet)
            {
                preview.Invalidate();
            }
        }

        private Button CreateImageMoveButton(Point location, string text, ImageTarget target, int horizontalStep, int verticalStep)
        {
            Button button = new Button { Location = location, Size = new Size(23, 23), Text = text, Tag = new ImageMove(target, horizontalStep, verticalStep) };
            button.Click += imageMoveButton_Click;
            return button;
        }

        private void imageMoveButton_Click(object sender, EventArgs e)
        {
            ImageMove move = (ImageMove)((Button)sender).Tag;
            HexagonDesign design = designs[activeDesignIndex];
            if (move.Target == ImageTarget.Hexagon)
            {
                design.HexagonImageOffsetX += move.HorizontalStep;
                design.HexagonImageOffsetY += move.VerticalStep;
            }
            else
            {
                design.FaceImageOffsetX += move.HorizontalStep;
                design.FaceImageOffsetY += move.VerticalStep;
            }

            if (hasNet)
            {
                preview.Invalidate();
            }
        }

        private void btnBackgroundColor_Click(object sender, EventArgs e)
        {
            bool isHexagon = sender == btnHexagonBackgroundColor;
            Color currentColor = isHexagon ? hexagonBackgroundColor : faceBackgroundColor;
            ShowColorPickerMenu((Control)sender, currentColor, delegate(Color selectedColor)
            {
                if (isHexagon)
                {
                    hexagonBackgroundColor = selectedColor;
                    pnlHexagonBackgroundColor.BackColor = hexagonBackgroundColor;
                }
                else
                {
                    faceBackgroundColor = selectedColor;
                    pnlFaceBackgroundColor.BackColor = faceBackgroundColor;
                }
                imageAppearanceChanged(sender, e);
            });
        }

        private void ShowColorPickerMenu(Control source, Color currentColor, Action<Color> colorSelected)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem chooseColorItem = new ToolStripMenuItem("Choose color...");
            ToolStripMenuItem pickFromScreenItem = new ToolStripMenuItem("Pick from screen...");

            chooseColorItem.Click += delegate
            {
                Color selectedColor;
                if (TrySelectColorFromDialog(currentColor, out selectedColor))
                {
                    colorSelected(selectedColor);
                }
            };
            pickFromScreenItem.Click += delegate { StartScreenColorSampler(colorSelected); };
            menu.Items.Add(chooseColorItem);
            menu.Items.Add(pickFromScreenItem);
            menu.Closed += delegate
            {
                BeginInvoke(new Action(menu.Dispose));
            };
            menu.Show(source, new Point(0, source.Height));
        }

        private bool TrySelectColorFromDialog(Color currentColor, out Color selectedColor)
        {
            selectedColor = currentColor;
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = currentColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                selectedColor = dialog.Color;
                return true;
            }
        }

        private void StartScreenColorSampler(Action<Color> colorSelected)
        {
            if (activeColorSampler != null)
            {
                return;
            }

            Sampler sampler = new Sampler();
            activeColorSampler = sampler;
            sampler.SamplerEvent += delegate(object sender, SamplerEventArgs e)
            {
                if (e.Kind == SamplerEventKind.Confirmed)
                {
                    CompleteScreenColorSampling(sampler, e.Color, colorSelected);
                }
                else if (e.Kind == SamplerEventKind.Cancelled)
                {
                    CompleteScreenColorSampling(sampler, null, null);
                }
            };

            try
            {
                sampler.Start();
            }
            catch (Exception exception)
            {
                sampler.Dispose();
                activeColorSampler = null;
                MessageBox.Show(this, "The screen color picker could not be started.\r\n\r\n" + exception.Message, "Unable to pick color", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CompleteScreenColorSampling(Sampler sampler, Color? selectedColor, Action<Color> colorSelected)
        {
            if (IsDisposed || Disposing)
            {
                sampler.Dispose();
                return;
            }

            BeginInvoke(new Action(delegate
            {
                if (activeColorSampler != sampler)
                {
                    return;
                }

                activeColorSampler = null;
                sampler.Dispose();
                if (selectedColor.HasValue && colorSelected != null)
                {
                    colorSelected(selectedColor.Value);
                }
            }));
        }

        private void btnSaveLayout_Click(object sender, EventArgs e)
        {
            SaveActiveDesign();
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Hexagon net layout (*.hexnet)|*.hexnet|All files (*.*)|*.*";
                dialog.DefaultExt = "hexnet";
                dialog.AddExtension = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(NetLayoutFile));
                    using (FileStream stream = File.Create(dialog.FileName))
                    {
                        serializer.WriteObject(stream, CreateLayoutFile());
                    }
                    layoutDirectory = Path.GetDirectoryName(dialog.FileName);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, "The layout could not be saved.\r\n\r\n" + exception.Message, "Save layout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnLoadLayout_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Hexagon net layout (*.hexnet)|*.hexnet|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                List<HexagonDesign> loadedDesigns = null;
                try
                {
                    NetLayoutFile layout;
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(NetLayoutFile));
                    using (FileStream stream = File.OpenRead(dialog.FileName))
                    {
                        layout = (NetLayoutFile)serializer.ReadObject(stream);
                    }
                    if (layout == null || layout.Designs == null || layout.Designs.Count == 0)
                    {
                        throw new InvalidDataException("This file does not contain a hexagon-net layout.");
                    }

                    loadedDesigns = new List<HexagonDesign>();
                    string layoutDirectory = Path.GetDirectoryName(dialog.FileName);
                    foreach (NetDesignFile savedDesign in layout.Designs)
                    {
                        loadedDesigns.Add(CreateDesignFromFile(savedDesign, layout, layoutDirectory));
                    }
                    ApplyLayoutFile(layout, loadedDesigns);
                    loadedDesigns = null;
                    this.layoutDirectory = layoutDirectory;
                }
                catch (Exception exception)
                {
                    if (loadedDesigns != null)
                    {
                        foreach (HexagonDesign design in loadedDesigns)
                        {
                            design.Dispose();
                        }
                    }
                    MessageBox.Show(this, "The layout could not be loaded.\r\n\r\n" + exception.Message, "Load layout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private NetLayoutFile CreateLayoutFile()
        {
            NetLayoutFile layout = new NetLayoutFile
            {
                MeasurementType = radSideLength.Checked ? 0 : (radVertexDiameter.Checked ? 1 : 2),
                HexagonValue = txtHexValue.Text,
                FaceHeight = txtFaceHeight.Text,
                FaceTolerance = txtFaceTolerance.Text,
                ConnectFaces = chkConnectFaces.Checked,
                StandsOnVertex = radStandsOnVertex.Checked,
                LineColorArgb = designs[0].LineColor.ToArgb(),
                LineThickness = designs[0].LineThickness,
                HasNet = hasNet,
                Designs = new List<NetDesignFile>()
            };
            foreach (HexagonDesign design in designs)
            {
                layout.Designs.Add(CreateDesignFile(design));
            }
            return layout;
        }

        private static NetDesignFile CreateDesignFile(HexagonDesign design)
        {
            return new NetDesignFile
            {
                Name = String.IsNullOrWhiteSpace(design.Name) ? null : design.Name,
                LineColorArgb = design.LineColor.ToArgb(),
                LineThickness = design.LineThickness,
                HeaderText = design.HeaderText,
                HeaderFontName = design.HeaderFontName,
                HeaderFontSize = design.HeaderFontSize,
                HeaderTextColorArgb = design.HeaderTextColor.ToArgb(),
                Subtext = design.Subtext,
                SubtextFontName = design.SubtextFontName,
                SubtextFontSize = design.SubtextFontSize,
                SubtextColorArgb = design.SubtextColor.ToArgb(),
                HexagonBackgroundColorArgb = design.HexagonBackgroundColor.ToArgb(),
                FaceBackgroundColorArgb = design.FaceBackgroundColor.ToArgb(),
                HexagonImagePath = design.HexagonImagePath,
                HexagonImageScale = design.HexagonImageScale,
                HexagonImageOffsetX = design.HexagonImageOffsetX,
                HexagonImageOffsetY = design.HexagonImageOffsetY,
                FaceImagePath = design.FaceImagePath,
                FaceImageScale = design.FaceImageScale,
                FaceImageOffsetX = design.FaceImageOffsetX,
                FaceImageOffsetY = design.FaceImageOffsetY,
                FaceImageFlip = design.FaceImageFlip,
                FaceText = design.FaceText,
                FaceTextFontName = design.FaceTextFontName,
                FaceTextFontSize = design.FaceTextFontSize,
                FaceTextColorArgb = design.FaceTextColor.ToArgb(),
                FaceTextAlignment = design.FaceTextAlignment,
                FaceTextFlipTopToBottom = design.FaceTextFlipTopToBottom,
                FaceTextWrapAround = design.FaceTextWrapAround,
                FaceTextLinePerFace = design.FaceTextLinePerFace,
                FaceTextStartAt = design.FaceTextStartAt
            };
        }

        private void ApplyLayoutFile(NetLayoutFile layout, List<HexagonDesign> loadedDesigns)
        {
            loadingLayout = true;
            try
            {
                radSideLength.Checked = layout.MeasurementType == 0;
                radVertexDiameter.Checked = layout.MeasurementType == 1;
                radEdgeDiameter.Checked = layout.MeasurementType == 2;
                txtHexValue.Text = layout.HexagonValue ?? String.Empty;
                txtFaceHeight.Text = layout.FaceHeight ?? String.Empty;
                txtFaceTolerance.Text = layout.FaceTolerance ?? String.Empty;
                chkConnectFaces.Checked = layout.ConnectFaces;
                radStandsOnVertex.Checked = layout.StandsOnVertex;
                radStandsOnEdge.Checked = !layout.StandsOnVertex;
                UpdateFaceOptions();
                UpdateMaximumNetCount();

                if (loadedDesigns.Count > (int)numHexagonCount.Maximum)
                {
                    throw new InvalidDataException("This layout contains more nets than fit on one A4 page with its saved measurements.");
                }

                foreach (HexagonDesign existingDesign in designs)
                {
                    existingDesign.Dispose();
                }
                designs.Clear();
                designs.AddRange(loadedDesigns);
                activeDesignIndex = 0;
                numHexagonCount.Value = designs.Count;
                RebuildDesignTabs();
                LoadActiveDesign();

                float parsedSide = 0;
                float parsedHeight = 0;
                float parsedTolerance = 0;
                hasNet = layout.HasNet && TryReadValues(out parsedSide, out parsedHeight, out parsedTolerance);
                if (hasNet)
                {
                    sideLengthMm = parsedSide;
                    lateralFaceHeightMm = parsedHeight;
                    faceToleranceMm = parsedTolerance;
                    UpdateA4Layout();
                }
                else
                {
                    btnPrint.Enabled = false;
                }
                preview.Invalidate();
            }
            finally
            {
                loadingLayout = false;
            }
        }

        private static Image LoadLayoutImage(string imagePath, string layoutDirectory)
        {
            if (String.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            string resolvedPath = Path.IsPathRooted(imagePath)
                ? imagePath
                : Path.Combine(layoutDirectory ?? Environment.CurrentDirectory, imagePath);
            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException("The image referenced by this layout could not be found: " + imagePath, resolvedPath);
            }

            using (Image source = Image.FromFile(resolvedPath))
            {
                return (Image)source.Clone();
            }
        }

        private static string GetImageDisplayName(string imagePath, Image image, string emptyText)
        {
            if (image == null)
            {
                return emptyText;
            }

            return String.IsNullOrEmpty(imagePath) ? "Image selected" : Path.GetFileName(imagePath);
        }

        private static HexagonDesign CreateDesignFromFile(NetDesignFile savedDesign, NetLayoutFile layout, string layoutDirectory)
        {
            if (savedDesign == null)
            {
                throw new InvalidDataException("The layout contains an invalid hexagon design.");
            }

            return new HexagonDesign
            {
                Name = savedDesign.Name,
                LineColor = Color.FromArgb(savedDesign.LineColorArgb ?? layout.LineColorArgb),
                LineThickness = Math.Max(0m, Math.Min(10m, savedDesign.LineThickness ?? layout.LineThickness)),
                HeaderText = savedDesign.HeaderText,
                HeaderFontName = savedDesign.HeaderFontName,
                HeaderFontSize = savedDesign.HeaderFontSize,
                HeaderTextColor = Color.FromArgb(savedDesign.HeaderTextColorArgb),
                Subtext = savedDesign.Subtext,
                SubtextFontName = savedDesign.SubtextFontName,
                SubtextFontSize = savedDesign.SubtextFontSize,
                SubtextColor = Color.FromArgb(savedDesign.SubtextColorArgb),
                HexagonBackgroundColor = Color.FromArgb(savedDesign.HexagonBackgroundColorArgb),
                FaceBackgroundColor = Color.FromArgb(savedDesign.FaceBackgroundColorArgb),
                HexagonImage = LoadLayoutImage(savedDesign.HexagonImagePath, layoutDirectory),
                HexagonImagePath = savedDesign.HexagonImagePath,
                HexagonImageScale = savedDesign.HexagonImageScale,
                HexagonImageOffsetX = savedDesign.HexagonImageOffsetX,
                HexagonImageOffsetY = savedDesign.HexagonImageOffsetY,
                FaceImage = LoadLayoutImage(savedDesign.FaceImagePath, layoutDirectory),
                FaceImagePath = savedDesign.FaceImagePath,
                FaceImageScale = savedDesign.FaceImageScale,
                FaceImageOffsetX = savedDesign.FaceImageOffsetX,
                FaceImageOffsetY = savedDesign.FaceImageOffsetY,
                FaceImageFlip = savedDesign.FaceImageFlip,
                FaceText = savedDesign.FaceText,
                FaceTextFontName = savedDesign.FaceTextFontName,
                FaceTextFontSize = savedDesign.FaceTextFontSize,
                FaceTextColor = Color.FromArgb(savedDesign.FaceTextColorArgb),
                FaceTextAlignment = savedDesign.FaceTextAlignment,
                FaceTextFlipTopToBottom = savedDesign.FaceTextFlipTopToBottom,
                FaceTextWrapAround = savedDesign.FaceTextWrapAround,
                FaceTextLinePerFace = savedDesign.FaceTextLinePerFace,
                FaceTextStartAt = savedDesign.FaceTextStartAt
            };
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            float parsedSideLength;
            float parsedFaceHeight;
            float parsedTolerance;
            if (!TryReadValues(out parsedSideLength, out parsedFaceHeight, out parsedTolerance))
            {
                MessageBox.Show(this, "Please enter a positive hexagon measurement, a non-negative face height, and a non-negative tolerance.", "Invalid dimensions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            sideLengthMm = parsedSideLength;
            lateralFaceHeightMm = parsedFaceHeight;
            faceToleranceMm = parsedTolerance;
            hasNet = true;
            UpdateMaximumNetCount();
            bool fitsOnA4 = TryGetA4Orientation(GetNetBounds(sideLengthMm, lateralFaceHeightMm, faceToleranceMm, radStandsOnVertex.Checked), out a4Landscape);
            btnPrint.Enabled = fitsOnA4;
            if (fitsOnA4)
            {
                ConfigureA4Page();
            }
            else
            {
                MessageBox.Show(this, "This net does not fit on one A4 page at the requested measurements. It is shown in the preview, but will not be scaled when printed.", "Net is too large for A4", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            UpdateConversionText();
            preview.Invalidate();
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (!TryGetA4Orientation(GetNetBounds(sideLengthMm, lateralFaceHeightMm, faceToleranceMm, radStandsOnVertex.Checked), out a4Landscape))
            {
                MessageBox.Show(this, "This net does not fit on one A4 page without changing its measurements.", "Net is too large for A4", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ConfigureA4Page();
            using (PrintDialog dialog = new PrintDialog())
            {
                dialog.Document = printDocument;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    printDocument.Print();
                }
            }
        }

        private void preview_Paint(object sender, PaintEventArgs e)
        {
            if (!hasNet)
            {
                TextRenderer.DrawText(e.Graphics, "Click Create to preview the net.", Font, preview.ClientRectangle, SystemColors.GrayText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            RectangleF bounds = GetNetBounds(sideLengthMm, lateralFaceHeightMm, faceToleranceMm, radStandsOnVertex.Checked);
            int columns;
            int rows;
            bool fitsOnA4 = TryGetA4Layout(bounds, out bool previewLandscape, out columns, out rows);
            if (!fitsOnA4)
            {
                columns = 1;
                rows = 1;
            }
            SizeF pageSize = GetA4PageSize(previewLandscape);
            const float previewPadding = 20;
            float scale = Math.Min((preview.ClientSize.Width - previewPadding * 2) / pageSize.Width, (preview.ClientSize.Height - previewPadding * 2) / pageSize.Height);
            if (scale <= 0)
            {
                return;
            }

            RectangleF page = new RectangleF(
                (preview.ClientSize.Width - pageSize.Width * scale) / 2f,
                (preview.ClientSize.Height - pageSize.Height * scale) / 2f,
                pageSize.Width * scale,
                pageSize.Height * scale);
            e.Graphics.FillRectangle(Brushes.White, page);
            e.Graphics.DrawRectangle(Pens.DimGray, page.X, page.Y, page.Width, page.Height);

            float marginMm = A4MarginHundredthsInch * MillimetresPerHundredthInch;
            float printableWidth = pageSize.Width - marginMm * 2f;
            float printableHeight = pageSize.Height - marginMm * 2f;
            const float gapMm = 5f;
            float gridWidth = columns * bounds.Width + Math.Max(0, columns - 1) * gapMm;
            float gridHeight = rows * bounds.Height + Math.Max(0, rows - 1) * gapMm;
            float gridLeft = marginMm + (printableWidth - gridWidth) / 2f;
            float gridTop = marginMm + (printableHeight - gridHeight) / 2f;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            GraphicsState savedState = e.Graphics.Save();
            e.Graphics.SetClip(page);
            for (int i = 0; i < designs.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                PointF origin = new PointF(
                    page.Left + (gridLeft + column * (bounds.Width + gapMm) - bounds.Left) * scale,
                    page.Top + (gridTop + row * (bounds.Height + gapMm) - bounds.Top) * scale);
                DrawNet(e.Graphics, sideLengthMm, lateralFaceHeightMm, faceToleranceMm, scale, origin, 1f, false, designs[i]);
            }
            e.Graphics.Restore(savedState);

            if (!fitsOnA4)
            {
                TextRenderer.DrawText(e.Graphics, "Does not fit on A4 at true size", Font, Rectangle.Round(page), Color.Firebrick, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            }
        }

        private void printDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            RectangleF bounds = GetNetBounds(sideLengthMm, lateralFaceHeightMm, faceToleranceMm, radStandsOnVertex.Checked);
            if (!FitsOnA4(bounds, e.PageSettings.Landscape))
            {
                e.Cancel = true;
                BeginInvoke((MethodInvoker)delegate
                {
                    MessageBox.Show(this, "The selected page orientation cannot contain this net at true size. Please use the automatically selected A4 orientation.", "Net is too large for A4", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
                return;
            }

            // OriginAtMargins moves (0,0) to the configured A4 margin.
            // Millimetres as the page unit make the PDF/printer output 1:1.
            e.Graphics.PageUnit = GraphicsUnit.Millimeter;
            SizeF pageSize = GetA4PageSize(e.PageSettings.Landscape);
            float marginMm = A4MarginHundredthsInch * MillimetresPerHundredthInch;
            float printableWidth = pageSize.Width - marginMm * 2f;
            float printableHeight = pageSize.Height - marginMm * 2f;
            const float gapMm = 5f;
            int columns = (int)Math.Floor((printableWidth + gapMm) / (bounds.Width + gapMm));
            int rows = (int)Math.Floor((printableHeight + gapMm) / (bounds.Height + gapMm));
            float gridWidth = columns * bounds.Width + Math.Max(0, columns - 1) * gapMm;
            float gridHeight = rows * bounds.Height + Math.Max(0, rows - 1) * gapMm;
            float gridLeft = (printableWidth - gridWidth) / 2f;
            float gridTop = (printableHeight - gridHeight) / 2f;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < designs.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                PointF origin = new PointF(
                    gridLeft + column * (bounds.Width + gapMm) - bounds.Left,
                    gridTop + row * (bounds.Height + gapMm) - bounds.Top);
                DrawNet(e.Graphics, sideLengthMm, lateralFaceHeightMm, faceToleranceMm, 1f, origin, 0f, true, designs[i]);
            }
            e.HasMorePages = false;
        }

        private void DrawNet(Graphics graphics, float side, float faceHeight, float tolerance, float scale, PointF origin, float minimumLineWidth, bool isPhysicalOutput, HexagonDesign design)
        {
            PointF[] hexagon = GetHexagon(side, radStandsOnVertex.Checked);
            PointF[] screenHexagon = ScalePoints(hexagon, scale, origin);

            // A thickness of 0 draws no lines at all, not a hairline.
            bool drawLines = design.LineThickness > 0;
            using (Pen outline = new Pen(design.LineColor, Math.Max(minimumLineWidth, (float)design.LineThickness * scale)))
            using (Brush hexagonBrush = new SolidBrush(design.HexagonBackgroundColor))
            using (Brush faceBrush = new SolidBrush(design.FaceBackgroundColor))
            {
                graphics.FillPolygon(hexagonBrush, screenHexagon);
                if (drawLines)
                {
                    graphics.DrawPolygon(outline, screenHexagon);
                }

                if (faceHeight > 0)
                {
                    if (chkConnectFaces.Checked)
                    {
                        for (int i = 0; i < hexagon.Length; i++)
                        {
                            PointF vertex = hexagon[(i + 1) % hexagon.Length];
                            PointF currentNormal = GetOutwardNormal(hexagon[i], vertex);
                            PointF nextNormal = GetOutwardNormal(vertex, hexagon[(i + 2) % hexagon.Length]);
                            PointF currentOuterCorner = new PointF(vertex.X + currentNormal.X * faceHeight, vertex.Y + currentNormal.Y * faceHeight);
                            PointF nextOuterCorner = new PointF(vertex.X + nextNormal.X * faceHeight, vertex.Y + nextNormal.Y * faceHeight);
                            PointF[] connector = new PointF[] { vertex, currentOuterCorner, nextOuterCorner };
                            graphics.FillPolygon(faceBrush, ScalePoints(connector, scale, origin));
                        }
                    }

                    for (int i = 0; i < hexagon.Length; i++)
                    {
                    PointF first = hexagon[i];
                    PointF second = hexagon[(i + 1) % hexagon.Length];
                    PointF outward = GetOutwardNormal(first, second);
                    PointF[] face = new PointF[]
                    {
                        first,
                        second,
                        new PointF(second.X + outward.X * faceHeight, second.Y + outward.Y * faceHeight),
                        new PointF(first.X + outward.X * faceHeight, first.Y + outward.Y * faceHeight)
                    };
                    PointF[] screenFace = ScalePoints(face, scale, origin);
                    graphics.FillPolygon(faceBrush, screenFace);
                    DrawFaceImage(graphics, first, second, outward, side, faceHeight, scale, origin, design);
                    DrawFaceText(graphics, i, first, second, outward, side, faceHeight, scale, origin, isPhysicalOutput, design);
                    if (drawLines)
                    {
                        graphics.DrawPolygon(outline, screenFace);
                    }

                    if (tolerance > 0)
                    {
                        PointF outerFirst = face[3];
                        PointF outerSecond = face[2];
                        PointF[] toleranceFace = new PointF[]
                        {
                            outerFirst,
                            outerSecond,
                            new PointF(outerSecond.X + outward.X * tolerance, outerSecond.Y + outward.Y * tolerance),
                            new PointF(outerFirst.X + outward.X * tolerance, outerFirst.Y + outward.Y * tolerance)
                        };
                        PointF[] screenToleranceFace = ScalePoints(toleranceFace, scale, origin);
                        graphics.FillPolygon(faceBrush, screenToleranceFace);
                        if (drawLines)
                        {
                            graphics.DrawPolygon(outline, screenToleranceFace);
                        }
                    }
                    }

                    if (chkConnectFaces.Checked && drawLines)
                    {
                        for (int i = 0; i < hexagon.Length; i++)
                        {
                            PointF vertex = hexagon[(i + 1) % hexagon.Length];
                            PointF currentNormal = GetOutwardNormal(hexagon[i], vertex);
                            PointF nextNormal = GetOutwardNormal(vertex, hexagon[(i + 2) % hexagon.Length]);
                            PointF currentOuterCorner = new PointF(vertex.X + currentNormal.X * faceHeight, vertex.Y + currentNormal.Y * faceHeight);
                            PointF nextOuterCorner = new PointF(vertex.X + nextNormal.X * faceHeight, vertex.Y + nextNormal.Y * faceHeight);
                            PointF[] connectorEdge = ScalePoints(new PointF[] { currentOuterCorner, nextOuterCorner }, scale, origin);
                            graphics.DrawLine(outline, connectorEdge[0], connectorEdge[1]);
                        }
                    }
                }
            }

            DrawHexagonImage(graphics, side, scale, origin, design);
            DrawHexagonText(graphics, scale, origin, isPhysicalOutput, design);
        }

        private void DrawHexagonImage(Graphics graphics, float side, float scale, PointF origin, HexagonDesign design)
        {
            if (design.HexagonImage == null || design.HexagonImage.Width == 0 || design.HexagonImage.Height == 0)
            {
                return;
            }

            // This square remains inside either regular-hexagon orientation.
            float maximumSize = side * 1.1f * scale * (float)design.HexagonImageScale / 100f;
            float imageScale = Math.Min(maximumSize / design.HexagonImage.Width, maximumSize / design.HexagonImage.Height);
            float imageWidth = design.HexagonImage.Width * imageScale;
            float imageHeight = design.HexagonImage.Height * imageScale;
            RectangleF imageBounds = new RectangleF(
                origin.X + (float)design.HexagonImageOffsetX * scale - imageWidth / 2f,
                origin.Y + (float)design.HexagonImageOffsetY * scale - imageHeight / 2f,
                imageWidth,
                imageHeight);

            GraphicsState savedState = graphics.Save();
            using (GraphicsPath hexagonClip = new GraphicsPath())
            {
                hexagonClip.AddPolygon(ScalePoints(GetHexagon(side, radStandsOnVertex.Checked), scale, origin));
                graphics.SetClip(hexagonClip, CombineMode.Intersect);
            }
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(design.HexagonImage, imageBounds);
            graphics.Restore(savedState);
        }

        private void DrawFaceImage(Graphics graphics, PointF first, PointF second, PointF outward, float side, float faceHeight, float scale, PointF origin, HexagonDesign design)
        {
            if (design.FaceImage == null || design.FaceImage.Width == 0 || design.FaceImage.Height == 0)
            {
                return;
            }

            float scalePercent = (float)design.FaceImageScale / 100f;
            float maximumWidth = side * 0.8f * scale * scalePercent;
            float maximumHeight = faceHeight * 0.8f * scale * scalePercent;
            float imageScale = Math.Min(maximumWidth / design.FaceImage.Width, maximumHeight / design.FaceImage.Height);
            float imageWidth = design.FaceImage.Width * imageScale;
            float imageHeight = design.FaceImage.Height * imageScale;
            PointF imageCenter = new PointF(
                origin.X + ((first.X + second.X) / 2f + outward.X * faceHeight / 2f) * scale,
                origin.Y + ((first.Y + second.Y) / 2f + outward.Y * faceHeight / 2f) * scale);
            float angle = (float)(Math.Atan2(second.Y - first.Y, second.X - first.X) * 180d / Math.PI);
            if (design.FaceImageFlip)
            {
                angle += 180f;
            }

            GraphicsState savedState = graphics.Save();
            graphics.TranslateTransform(imageCenter.X, imageCenter.Y);
            graphics.RotateTransform(angle);
            graphics.SetClip(new RectangleF(-side * scale / 2f, -faceHeight * scale / 2f, side * scale, faceHeight * scale), CombineMode.Intersect);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(design.FaceImage, new RectangleF(
                (float)design.FaceImageOffsetX * scale - imageWidth / 2f,
                (float)design.FaceImageOffsetY * scale - imageHeight / 2f,
                imageWidth,
                imageHeight));
            graphics.Restore(savedState);
        }

        private void DrawFaceText(Graphics graphics, int faceIndex, PointF first, PointF second, PointF outward, float side, float faceHeight, float scale, PointF origin, bool isPhysicalOutput, HexagonDesign design)
        {
            if (String.IsNullOrWhiteSpace(design.FaceText))
            {
                return;
            }

            PointF faceCenter = new PointF(
                origin.X + ((first.X + second.X) / 2f + outward.X * faceHeight / 2f) * scale,
                origin.Y + ((first.Y + second.Y) / 2f + outward.Y * faceHeight / 2f) * scale);
            float angle = (float)(Math.Atan2(second.Y - first.Y, second.X - first.X) * 180d / Math.PI);
            float faceWidth = side * scale;
            float faceTextHeight = faceHeight * scale;

            GraphicsState savedState = graphics.Save();
            graphics.TranslateTransform(faceCenter.X, faceCenter.Y);
            graphics.RotateTransform(angle);
            if (design.FaceTextFlipTopToBottom)
            {
                graphics.RotateTransform(180f, MatrixOrder.Prepend);
            }
            graphics.SetClip(new RectangleF(-faceWidth / 2f, -faceTextHeight / 2f, faceWidth, faceTextHeight), CombineMode.Intersect);

            RectangleF textArea;
            string textToDraw = design.FaceText;
            if (design.FaceTextLinePerFace)
            {
                int startFaceIndex = Math.Max(0, Math.Min(5, design.FaceTextStartAt - 1));
                int lineIndex = (faceIndex - startFaceIndex + 6) % 6;
                string[] lines = design.FaceText.Replace("\r\n", "\n").Replace('\r', '\n').Split(new[] { '\n' });
                textToDraw = lineIndex < lines.Length ? lines[lineIndex] : String.Empty;
                textArea = new RectangleF(-faceWidth / 2f, -faceTextHeight / 2f, faceWidth, faceTextHeight);
            }
            else if (design.FaceTextWrapAround)
            {
                int startFaceIndex = Math.Max(0, Math.Min(5, design.FaceTextStartAt - 1));
                int positionInText = (faceIndex - startFaceIndex + 6) % 6;
                textArea = new RectangleF(
                    -positionInText * faceWidth - faceWidth / 2f,
                    -faceTextHeight / 2f,
                    faceWidth * 6f,
                    faceTextHeight);
            }
            else
            {
                textArea = new RectangleF(-faceWidth / 2f, -faceTextHeight / 2f, faceWidth, faceTextHeight);
            }

            if (!String.IsNullOrEmpty(textToDraw))
            {
                RichTextStyle defaultStyle = new RichTextStyle(design.FaceTextFontName, design.FaceTextFontSize, design.FaceTextColor);
                DrawStyledText(graphics, textToDraw, defaultStyle, textArea, GetStringAlignment(design.FaceTextAlignment), scale, isPhysicalOutput, GetTextImage);
            }
            graphics.Restore(savedState);
        }

        private Image GetTextImage(string source)
        {
            if (String.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            source = source.Trim();
            Image image;
            if (textImages.TryGetValue(source, out image))
            {
                return image;
            }

            try
            {
                if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    int dataStart = source.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
                    if (dataStart < 0)
                    {
                        return null;
                    }
                    using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(source.Substring(dataStart + 7))))
                    using (Image decoded = Image.FromStream(stream))
                    {
                        image = new Bitmap(decoded);
                    }
                }
                else
                {
                    string path = source;
                    Uri uri;
                    if (source.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(source, UriKind.Absolute, out uri))
                    {
                        path = uri.LocalPath;
                    }
                    if (!Path.IsPathRooted(path))
                    {
                        path = Path.Combine(layoutDirectory ?? Environment.CurrentDirectory, path);
                    }
                    // Missing files are not cached, so a path that is still
                    // being typed shows up as soon as it is complete.
                    if (!File.Exists(path))
                    {
                        return null;
                    }
                    using (Image loaded = Image.FromFile(path))
                    {
                        image = new Bitmap(loaded);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            textImages[source] = image;
            return image;
        }

        // Converts an <img> width/height (points, or millimetres with an
        // "mm" suffix) into drawing units of the current output.
        private static float GetImageLength(float points, float scale, bool isPhysicalOutput)
        {
            float millimetres = points * 25.4f / 72f;
            return isPhysicalOutput ? millimetres : millimetres * scale;
        }

        // Draws styled text vertically centred in textArea.
        private static void DrawStyledText(Graphics graphics, string text, RichTextStyle defaultStyle, RectangleF textArea, StringAlignment alignment, float scale, bool isPhysicalOutput, Func<string, Image> imageResolver)
        {
            using (MeasuredRichText measured = MeasureStyledText(graphics, text, defaultStyle, scale, isPhysicalOutput, imageResolver))
            {
                DrawMeasuredStyledText(graphics, measured, textArea.X, textArea.Width, textArea.Y + (textArea.Height - measured.Height) / 2f, alignment, isPhysicalOutput);
            }
        }

        // Parses and measures styled text. The result owns its fonts and must be disposed.
        private static MeasuredRichText MeasureStyledText(Graphics graphics, string text, RichTextStyle defaultStyle, float scale, bool isPhysicalOutput, Func<string, Image> imageResolver)
        {
            MeasuredRichText measured = new MeasuredRichText();
            try
            {
                foreach (RichTextLine line in ParseStyledText(text, defaultStyle))
                {
                    MeasuredRichTextLine measuredLine = new MeasuredRichTextLine();
                    foreach (RichTextRun run in line.Runs)
                    {
                        Font font = CreateTextFont(run.Style.FontName, run.Style.FontSize, scale, graphics, isPhysicalOutput, run.Style.FontStyle);
                        float fontHeight = font.GetHeight(graphics);
                        float fontAscent = GetFontAscent(font, fontHeight);
                        MeasuredRichTextRun measuredRun;
                        if (run.IsImage)
                        {
                            // By default an image fills the line box of the
                            // surrounding text and keeps its aspect ratio.
                            Image image = imageResolver == null ? null : imageResolver(run.ImageSource);
                            float aspect = image != null && image.Width > 0 && image.Height > 0 ? (float)image.Width / image.Height : 1f;
                            float height = run.ImageHeight > 0 ? GetImageLength(run.ImageHeight, scale, isPhysicalOutput) : 0f;
                            float width = run.ImageWidth > 0 ? GetImageLength(run.ImageWidth, scale, isPhysicalOutput) : 0f;
                            if (height <= 0 && width <= 0)
                            {
                                height = fontHeight;
                            }
                            if (height <= 0)
                            {
                                height = width / aspect;
                            }
                            if (width <= 0)
                            {
                                width = height * aspect;
                            }
                            // The image bottom sits on the text's descent line.
                            float descent = fontHeight - fontAscent;
                            measuredRun = new MeasuredRichTextRun(run, font, width) { Image = image, ImageHeight = height, Ascent = height - descent, Descent = descent };
                        }
                        else
                        {
                            using (StringFormat measureFormat = CreateStyledTextFormat())
                            {
                                measuredRun = new MeasuredRichTextRun(run, font, graphics.MeasureString(run.Text, font, PointF.Empty, measureFormat).Width);
                            }
                            measuredRun.Ascent = fontAscent;
                            measuredRun.Descent = fontHeight - fontAscent;
                        }
                        measuredLine.Runs.Add(measuredRun);
                        measuredLine.Width += measuredRun.Width;
                        measuredLine.Ascent = Math.Max(measuredLine.Ascent, measuredRun.Ascent);
                        measuredLine.Descent = Math.Max(measuredLine.Descent, measuredRun.Descent);
                    }
                    measuredLine.Height = measuredLine.Ascent + measuredLine.Descent;

                    if (measuredLine.Runs.Count == 0)
                    {
                        using (Font font = CreateTextFont(defaultStyle.FontName, defaultStyle.FontSize, scale, graphics, isPhysicalOutput, defaultStyle.FontStyle))
                        {
                            measuredLine.Height = font.GetHeight(graphics);
                        }
                    }

                    measured.Height += measuredLine.Height;
                    measured.Lines.Add(measuredLine);
                }
                return measured;
            }
            catch
            {
                measured.Dispose();
                throw;
            }
        }

        // Draws measured text from top; each line is aligned within [left, left + width].
        private static void DrawMeasuredStyledText(Graphics graphics, MeasuredRichText measured, float left, float width, float top, StringAlignment alignment, bool isPhysicalOutput)
        {
            float y = top;
            foreach (MeasuredRichTextLine line in measured.Lines)
            {
                float x = left;
                if (alignment == StringAlignment.Center)
                {
                    x += (width - line.Width) / 2f;
                }
                else if (alignment == StringAlignment.Far)
                {
                    x += width - line.Width;
                }

                // Runs share a baseline, so mixed sizes and images line up.
                float baseline = y + line.Ascent;
                foreach (MeasuredRichTextRun run in line.Runs)
                {
                    if (run.Run.IsImage)
                    {
                        RectangleF imageBounds = new RectangleF(x, baseline - run.Ascent, run.Width, run.ImageHeight);
                        if (run.Image != null)
                        {
                            InterpolationMode previousMode = graphics.InterpolationMode;
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.DrawImage(run.Image, imageBounds);
                            graphics.InterpolationMode = previousMode;
                        }
                        else if (!isPhysicalOutput)
                        {
                            // Missing images show a placeholder in the preview only.
                            using (Pen placeholder = new Pen(Color.Firebrick, 1f) { DashStyle = DashStyle.Dash })
                            {
                                graphics.DrawRectangle(placeholder, imageBounds.X, imageBounds.Y, imageBounds.Width, imageBounds.Height);
                            }
                        }
                    }
                    else
                    {
                        using (Brush brush = new SolidBrush(run.Run.Style.Color))
                        using (StringFormat drawFormat = CreateStyledTextFormat())
                        {
                            graphics.DrawString(run.Run.Text, run.Font, brush, new PointF(x, baseline - run.Ascent), drawFormat);
                        }
                    }
                    x += run.Width;
                }
                y += line.Height;
            }
        }

        private static float GetFontAscent(Font font, float fontHeight)
        {
            FontFamily family = font.FontFamily;
            int lineSpacing = family.GetLineSpacing(font.Style);
            return lineSpacing > 0 ? fontHeight * family.GetCellAscent(font.Style) / lineSpacing : fontHeight;
        }

        private static StringFormat CreateStyledTextFormat()
        {
            StringFormat format = new StringFormat(StringFormat.GenericTypographic);
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            return format;
        }

        private static List<RichTextLine> ParseStyledText(string text, RichTextStyle defaultStyle)
        {
            List<RichTextRun> runs = new List<RichTextRun>();
            List<RichTextTag> activeTags = new List<RichTextTag>();
            int position = 0;
            while (position < text.Length)
            {
                int tagStart = text.IndexOfAny(new[] { '[', '<' }, position);
                if (tagStart < 0)
                {
                    AddRichTextRun(runs, text.Substring(position), GetRichTextStyle(defaultStyle, activeTags));
                    break;
                }

                if (tagStart > position)
                {
                    AddRichTextRun(runs, text.Substring(position, tagStart - position), GetRichTextStyle(defaultStyle, activeTags));
                }

                if (text[tagStart] == '<')
                {
                    int htmlTagEnd = text.IndexOf('>', tagStart + 1);
                    string htmlTag = htmlTagEnd < 0 ? null : text.Substring(tagStart + 1, htmlTagEnd - tagStart - 1);
                    RichTextRun imageRun;
                    if (htmlTag != null && TryParseImageTag(htmlTag, GetRichTextStyle(defaultStyle, activeTags), out imageRun))
                    {
                        runs.Add(imageRun);
                        position = htmlTagEnd + 1;
                    }
                    else if (htmlTag != null && Regex.IsMatch(htmlTag, @"^\s*/\s*img\s*$", RegexOptions.IgnoreCase))
                    {
                        position = htmlTagEnd + 1;
                    }
                    else
                    {
                        // Not an image tag: keep the '<' as text.
                        AddRichTextRun(runs, "<", GetRichTextStyle(defaultStyle, activeTags));
                        position = tagStart + 1;
                    }
                    continue;
                }

                int tagEnd = text.IndexOf(']', tagStart + 1);
                if (tagEnd < 0)
                {
                    AddRichTextRun(runs, text.Substring(tagStart), GetRichTextStyle(defaultStyle, activeTags));
                    break;
                }

                string tagText = text.Substring(tagStart + 1, tagEnd - tagStart - 1);
                RichTextTag tag;
                if (TryParseRichTextTag(tagText, out tag))
                {
                    activeTags.Add(tag);
                }
                else if (!TryCloseRichTextTag(tagText, activeTags))
                {
                    AddRichTextRun(runs, text.Substring(tagStart, tagEnd - tagStart + 1), GetRichTextStyle(defaultStyle, activeTags));
                }
                position = tagEnd + 1;
            }

            List<RichTextLine> lines = new List<RichTextLine> { new RichTextLine() };
            foreach (RichTextRun run in runs)
            {
                if (run.IsImage)
                {
                    lines[lines.Count - 1].Runs.Add(run);
                    continue;
                }

                string normalizedText = run.Text.Replace("\r\n", "\n").Replace('\r', '\n');
                string[] parts = normalizedText.Split(new[] { '\n' });
                for (int i = 0; i < parts.Length; i++)
                {
                    AddRichTextRun(lines[lines.Count - 1].Runs, parts[i], run.Style);
                    if (i < parts.Length - 1)
                    {
                        lines.Add(new RichTextLine());
                    }
                }
            }
            return lines;
        }

        private static readonly Regex ImageTagPattern = new Regex(@"^\s*img(?:\s+(?<attributes>.*?))?\s*/?\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex HtmlAttributePattern = new Regex(@"(?<name>[\w-]+)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s""'>]+))", RegexOptions.Singleline);

        // Parses <img src="..." width="..." height="...">. Sizes are points,
        // or millimetres with an "mm" suffix.
        private static bool TryParseImageTag(string tagText, RichTextStyle style, out RichTextRun run)
        {
            run = null;
            Match match = ImageTagPattern.Match(tagText);
            if (!match.Success)
            {
                return false;
            }

            string source = null;
            float width = 0f;
            float height = 0f;
            foreach (Match attribute in HtmlAttributePattern.Matches(match.Groups["attributes"].Value))
            {
                string name = attribute.Groups["name"].Value.ToLowerInvariant();
                string value = attribute.Groups["value"].Value;
                if (name == "src")
                {
                    source = value;
                }
                else if (name == "width")
                {
                    width = ParseImageLength(value);
                }
                else if (name == "height")
                {
                    height = ParseImageLength(value);
                }
            }
            if (String.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            run = new RichTextRun(String.Empty, style) { IsImage = true, ImageSource = source, ImageWidth = width, ImageHeight = height };
            return true;
        }

        private static float ParseImageLength(string value)
        {
            string text = (value ?? String.Empty).Trim().ToLowerInvariant();
            float factor = 1f;
            if (text.EndsWith("mm"))
            {
                factor = 72f / 25.4f;
                text = text.Substring(0, text.Length - 2);
            }
            else if (text.EndsWith("pt") || text.EndsWith("px"))
            {
                text = text.Substring(0, text.Length - 2);
            }

            float length;
            if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out length) || length <= 0)
            {
                return 0f;
            }
            return Math.Min(1000f, length * factor);
        }

        private static void AddRichTextRun(List<RichTextRun> runs, string text, RichTextStyle style)
        {
            if (String.IsNullOrEmpty(text))
            {
                return;
            }
            runs.Add(new RichTextRun(text, style));
        }

        private static RichTextStyle GetRichTextStyle(RichTextStyle defaultStyle, List<RichTextTag> activeTags)
        {
            RichTextStyle style = defaultStyle.Clone();
            foreach (RichTextTag tag in activeTags)
            {
                if (tag.Name == "b")
                {
                    style.Bold = true;
                }
                else if (tag.Name == "i")
                {
                    style.Italic = true;
                }
                else if (tag.Name == "u")
                {
                    style.Underline = true;
                }
                else if (tag.Name == "size")
                {
                    style.FontSize = tag.Size;
                }
                else if (tag.Name == "font")
                {
                    style.FontName = tag.Value;
                }
                else if (tag.Name == "color")
                {
                    style.Color = tag.Color;
                }
            }
            return style;
        }

        private static bool TryParseRichTextTag(string tagText, out RichTextTag tag)
        {
            tag = null;
            string trimmedTag = tagText.Trim();
            if (String.Equals(trimmedTag, "b", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(trimmedTag, "i", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(trimmedTag, "u", StringComparison.OrdinalIgnoreCase))
            {
                tag = new RichTextTag(trimmedTag.ToLowerInvariant());
                return true;
            }

            int equalsIndex = trimmedTag.IndexOf('=');
            if (equalsIndex <= 0)
            {
                return false;
            }
            string name = trimmedTag.Substring(0, equalsIndex).Trim().ToLowerInvariant();
            string value = trimmedTag.Substring(equalsIndex + 1).Trim().Trim('"', '\'');
            if (name == "size")
            {
                float size;
                if (Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out size) && size >= 1f && size <= 200f)
                {
                    tag = new RichTextTag(name) { Size = size };
                    return true;
                }
            }
            else if (name == "font" && !String.IsNullOrWhiteSpace(value))
            {
                tag = new RichTextTag(name) { Value = value };
                return true;
            }
            else if (name == "color")
            {
                try
                {
                    tag = new RichTextTag(name) { Color = ColorTranslator.FromHtml(value) };
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        private static bool TryCloseRichTextTag(string tagText, List<RichTextTag> activeTags)
        {
            string name = tagText.Trim();
            if (!name.StartsWith("/", StringComparison.Ordinal) || name.Length == 1)
            {
                return false;
            }
            name = name.Substring(1).Trim().ToLowerInvariant();
            for (int i = activeTags.Count - 1; i >= 0; i--)
            {
                if (activeTags[i].Name == name)
                {
                    activeTags.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        private static StringAlignment GetStringAlignment(int selectedIndex)
        {
            if (selectedIndex == 1)
            {
                return StringAlignment.Near;
            }
            if (selectedIndex == 2)
            {
                return StringAlignment.Far;
            }
            return StringAlignment.Center;
        }

        private static NumericUpDown CreateImageScaleInput(Point location)
        {
            return new NumericUpDown
            {
                Location = location,
                Size = new Size(69, 20),
                Minimum = 1,
                Maximum = 500,
                Value = 100
            };
        }

        private static void PopulateFontOptions(ComboBox fontBox, ComboBox fontSizeBox, int defaultFontSize)
        {
            foreach (FontFamily family in FontFamily.Families)
            {
                fontBox.Items.Add(family.Name);
            }
            fontBox.Sorted = true;
            fontBox.SelectedItem = SystemFonts.DefaultFont.FontFamily.Name;
            if (fontBox.SelectedIndex < 0 && fontBox.Items.Count > 0)
            {
                fontBox.SelectedIndex = 0;
            }

            int[] sizes = new int[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48 };
            foreach (int size in sizes)
            {
                fontSizeBox.Items.Add(size.ToString(CultureInfo.InvariantCulture));
            }
            fontSizeBox.SelectedItem = defaultFontSize.ToString(CultureInfo.InvariantCulture);
        }

        private GroupBox CreateTextGroup(string title, out TextBox textBox, out ComboBox fontBox, out ComboBox fontSizeBox, out Button textColorButton, out Panel textColorPreview, int defaultFontSize, bool multiline)
        {
            int verticalOffset = multiline ? 40 : 0;
            GroupBox group = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 112 + verticalOffset,
                Text = title
            };

            textBox = new TextBox
            {
                Location = new Point(12, 21),
                Size = new Size(206, multiline ? 60 : 20),
                Multiline = multiline,
                WordWrap = false,
                ScrollBars = multiline ? ScrollBars.Both : ScrollBars.None
            };
            Label fontLabel = new Label { AutoSize = true, Location = new Point(12, 51 + verticalOffset), Text = "Font:" };
            fontBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(48, 48 + verticalOffset), Size = new Size(116, 21) };
            Label sizeLabel = new Label { AutoSize = true, Location = new Point(169, 51 + verticalOffset), Text = "Size:" };
            fontSizeBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(202, 48 + verticalOffset), Size = new Size(43, 21) };
            textColorButton = new Button { Location = new Point(12, 77 + verticalOffset), Size = new Size(100, 23), Text = "Text color..." };
            textColorPreview = new Panel { Location = new Point(119, 78 + verticalOffset), Size = new Size(28, 21), BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle };
            PopulateFontOptions(fontBox, fontSizeBox, defaultFontSize);

            group.Controls.AddRange(new Control[]
            {
                CreateHelpIcon(StyledTextHelp), textBox, fontLabel, fontBox, sizeLabel, fontSizeBox, textColorButton, textColorPreview,
                CreateInsertImageButton(new Point(170, 77 + verticalOffset), textBox)
            });
            return group;
        }

        private void DrawHexagonText(Graphics graphics, float scale, PointF origin, bool isPhysicalOutput, HexagonDesign design)
        {
            // Header and text are stacked and centred together on the hexagon.
            List<MeasuredRichText> blocks = new List<MeasuredRichText>();
            try
            {
                if (!String.IsNullOrWhiteSpace(design.HeaderText))
                {
                    blocks.Add(MeasureStyledText(graphics, design.HeaderText, new RichTextStyle(design.HeaderFontName, design.HeaderFontSize, design.HeaderTextColor), scale, isPhysicalOutput, GetTextImage));
                }
                if (!String.IsNullOrWhiteSpace(design.Subtext))
                {
                    blocks.Add(MeasureStyledText(graphics, design.Subtext, new RichTextStyle(design.SubtextFontName, design.SubtextFontSize, design.SubtextColor), scale, isPhysicalOutput, GetTextImage));
                }

                float totalHeight = 0f;
                foreach (MeasuredRichText block in blocks)
                {
                    totalHeight += block.Height;
                }

                float top = origin.Y - totalHeight / 2f;
                foreach (MeasuredRichText block in blocks)
                {
                    DrawMeasuredStyledText(graphics, block, origin.X, 0f, top, StringAlignment.Center, isPhysicalOutput);
                    top += block.Height;
                }
            }
            finally
            {
                foreach (MeasuredRichText block in blocks)
                {
                    block.Dispose();
                }
            }
        }

        private static Font CreateTextFont(string fontName, float fontSize, float scale, Graphics graphics, bool isPhysicalOutput, FontStyle fontStyle)
        {
            // Point sizes preserve their real-world dimensions on the printed
            // A4 page. On screen they are converted to the preview's scale.
            float drawSize = isPhysicalOutput ? fontSize : fontSize * scale * 25.4f / graphics.DpiY;
            try
            {
                return new Font(fontName ?? SystemFonts.DefaultFont.FontFamily.Name, drawSize, fontStyle, GraphicsUnit.Point);
            }
            catch (ArgumentException)
            {
                return new Font(SystemFonts.DefaultFont.FontFamily, drawSize, fontStyle, GraphicsUnit.Point);
            }
        }

        private sealed class RichTextStyle
        {
            public string FontName;
            public float FontSize;
            public Color Color;
            public bool Bold;
            public bool Italic;
            public bool Underline;

            public FontStyle FontStyle
            {
                get
                {
                    FontStyle style = FontStyle.Regular;
                    if (Bold)
                    {
                        style |= FontStyle.Bold;
                    }
                    if (Italic)
                    {
                        style |= FontStyle.Italic;
                    }
                    if (Underline)
                    {
                        style |= FontStyle.Underline;
                    }
                    return style;
                }
            }

            public RichTextStyle(string fontName, float fontSize, Color color)
            {
                FontName = fontName;
                FontSize = fontSize;
                Color = color;
            }

            public RichTextStyle Clone()
            {
                return new RichTextStyle(FontName, FontSize, Color)
                {
                    Bold = Bold,
                    Italic = Italic,
                    Underline = Underline
                };
            }
        }

        private sealed class RichTextTag
        {
            public string Name;
            public string Value;
            public float Size;
            public Color Color;

            public RichTextTag(string name)
            {
                Name = name;
            }
        }

        private sealed class RichTextRun
        {
            public string Text;
            public RichTextStyle Style;
            public bool IsImage;
            public string ImageSource;
            // Requested image size in points; 0 means automatic.
            public float ImageWidth;
            public float ImageHeight;

            public RichTextRun(string text, RichTextStyle style)
            {
                Text = text;
                Style = style;
            }
        }

        private sealed class RichTextLine
        {
            public readonly List<RichTextRun> Runs = new List<RichTextRun>();
        }

        private sealed class MeasuredRichTextRun
        {
            public RichTextRun Run;
            public Font Font;
            public float Width;
            public float Ascent;
            public float Descent;
            public Image Image;
            public float ImageHeight;

            public MeasuredRichTextRun(RichTextRun run, Font font, float width)
            {
                Run = run;
                Font = font;
                Width = width;
            }
        }

        private sealed class MeasuredRichText : IDisposable
        {
            public readonly List<MeasuredRichTextLine> Lines = new List<MeasuredRichTextLine>();
            public float Height;

            public void Dispose()
            {
                foreach (MeasuredRichTextLine line in Lines)
                {
                    foreach (MeasuredRichTextRun run in line.Runs)
                    {
                        run.Font.Dispose();
                    }
                }
                Lines.Clear();
            }
        }

        private sealed class MeasuredRichTextLine
        {
            public readonly List<MeasuredRichTextRun> Runs = new List<MeasuredRichTextRun>();
            public float Width;
            public float Height;
            public float Ascent;
            public float Descent;
        }

        [DataContract]
        private sealed class NetLayoutFile
        {
            [DataMember] public int MeasurementType { get; set; }
            [DataMember] public string HexagonValue { get; set; }
            [DataMember] public string FaceHeight { get; set; }
            [DataMember] public string FaceTolerance { get; set; }
            [DataMember] public bool ConnectFaces { get; set; }
            [DataMember] public bool StandsOnVertex { get; set; }
            [DataMember] public int LineColorArgb { get; set; }
            [DataMember] public decimal LineThickness { get; set; }
            [DataMember] public bool HasNet { get; set; }
            [DataMember] public List<NetDesignFile> Designs { get; set; }
        }

        [DataContract]
        private sealed class NetDesignFile
        {
            // Older layouts store the line style once in NetLayoutFile.
            [DataMember(EmitDefaultValue = false)] public string Name { get; set; }
            [DataMember(EmitDefaultValue = false)] public int? LineColorArgb { get; set; }
            [DataMember(EmitDefaultValue = false)] public decimal? LineThickness { get; set; }
            [DataMember] public string HeaderText { get; set; }
            [DataMember] public string HeaderFontName { get; set; }
            [DataMember] public int HeaderFontSize { get; set; }
            [DataMember] public int HeaderTextColorArgb { get; set; }
            [DataMember] public string Subtext { get; set; }
            [DataMember] public string SubtextFontName { get; set; }
            [DataMember] public int SubtextFontSize { get; set; }
            [DataMember] public int SubtextColorArgb { get; set; }
            [DataMember] public int HexagonBackgroundColorArgb { get; set; }
            [DataMember] public int FaceBackgroundColorArgb { get; set; }
            [DataMember(EmitDefaultValue = false)] public string HexagonImagePath { get; set; }
            [DataMember] public decimal HexagonImageScale { get; set; }
            [DataMember] public decimal HexagonImageOffsetX { get; set; }
            [DataMember] public decimal HexagonImageOffsetY { get; set; }
            [DataMember(EmitDefaultValue = false)] public string FaceImagePath { get; set; }
            [DataMember] public decimal FaceImageScale { get; set; }
            [DataMember] public decimal FaceImageOffsetX { get; set; }
            [DataMember] public decimal FaceImageOffsetY { get; set; }
            [DataMember] public bool FaceImageFlip { get; set; }
            [DataMember] public string FaceText { get; set; }
            [DataMember] public string FaceTextFontName { get; set; }
            [DataMember] public int FaceTextFontSize { get; set; }
            [DataMember] public int FaceTextColorArgb { get; set; }
            [DataMember] public int FaceTextAlignment { get; set; }
            [DataMember] public bool FaceTextFlipTopToBottom { get; set; }
            [DataMember] public bool FaceTextWrapAround { get; set; }
            [DataMember] public bool FaceTextLinePerFace { get; set; }
            [DataMember] public int FaceTextStartAt { get; set; }
        }

        private sealed class HexagonDesign : IDisposable
        {
            // Empty means the tab shows its numbered default name.
            public string Name;
            public Color LineColor = Color.Gray;
            public decimal LineThickness = 0.35m;
            public string HeaderText;
            public string HeaderFontName;
            public int HeaderFontSize;
            public Color HeaderTextColor;
            public string Subtext;
            public string SubtextFontName;
            public int SubtextFontSize;
            public Color SubtextColor;
            public Color HexagonBackgroundColor;
            public Color FaceBackgroundColor;
            public Image HexagonImage;
            public string HexagonImagePath;
            public decimal HexagonImageScale;
            public decimal HexagonImageOffsetX;
            public decimal HexagonImageOffsetY;
            public Image FaceImage;
            public string FaceImagePath;
            public decimal FaceImageScale;
            public decimal FaceImageOffsetX;
            public decimal FaceImageOffsetY;
            public bool FaceImageFlip;
            public string FaceText;
            public string FaceTextFontName;
            public int FaceTextFontSize;
            public Color FaceTextColor;
            public int FaceTextAlignment;
            public bool FaceTextFlipTopToBottom;
            public bool FaceTextWrapAround;
            public bool FaceTextLinePerFace;
            public int FaceTextStartAt;

            public HexagonDesign Clone()
            {
                return new HexagonDesign
                {
                    Name = Name,
                    LineColor = LineColor,
                    LineThickness = LineThickness,
                    HeaderText = HeaderText,
                    HeaderFontName = HeaderFontName,
                    HeaderFontSize = HeaderFontSize,
                    HeaderTextColor = HeaderTextColor,
                    Subtext = Subtext,
                    SubtextFontName = SubtextFontName,
                    SubtextFontSize = SubtextFontSize,
                    SubtextColor = SubtextColor,
                    HexagonBackgroundColor = HexagonBackgroundColor,
                    FaceBackgroundColor = FaceBackgroundColor,
                    HexagonImage = HexagonImage == null ? null : (Image)HexagonImage.Clone(),
                    HexagonImagePath = HexagonImagePath,
                    HexagonImageScale = HexagonImageScale,
                    HexagonImageOffsetX = HexagonImageOffsetX,
                    HexagonImageOffsetY = HexagonImageOffsetY,
                    FaceImage = FaceImage == null ? null : (Image)FaceImage.Clone(),
                    FaceImagePath = FaceImagePath,
                    FaceImageScale = FaceImageScale,
                    FaceImageOffsetX = FaceImageOffsetX,
                    FaceImageOffsetY = FaceImageOffsetY,
                    FaceImageFlip = FaceImageFlip,
                    FaceText = FaceText,
                    FaceTextFontName = FaceTextFontName,
                    FaceTextFontSize = FaceTextFontSize,
                    FaceTextColor = FaceTextColor,
                    FaceTextAlignment = FaceTextAlignment,
                    FaceTextFlipTopToBottom = FaceTextFlipTopToBottom,
                    FaceTextWrapAround = FaceTextWrapAround,
                    FaceTextLinePerFace = FaceTextLinePerFace,
                    FaceTextStartAt = FaceTextStartAt
                };
            }

            public void Dispose()
            {
                if (HexagonImage != null)
                {
                    HexagonImage.Dispose();
                    HexagonImage = null;
                    HexagonImagePath = null;
                }
                if (FaceImage != null)
                {
                    FaceImage.Dispose();
                    FaceImage = null;
                    FaceImagePath = null;
                }
            }
        }

        private enum ImageTarget
        {
            Hexagon,
            Face
        }

        private sealed class ImageMove
        {
            public ImageTarget Target { get; private set; }
            public int HorizontalStep { get; private set; }
            public int VerticalStep { get; private set; }

            public ImageMove(ImageTarget target, int horizontalStep, int verticalStep)
            {
                Target = target;
                HorizontalStep = horizontalStep;
                VerticalStep = verticalStep;
            }
        }

        private void ConfigureA4Page()
        {
            PaperSize a4Paper = null;
            foreach (PaperSize paper in printDocument.PrinterSettings.PaperSizes)
            {
                if (paper.Kind == PaperKind.A4)
                {
                    a4Paper = paper;
                    break;
                }
            }

            // Some PDF drivers do not advertise their paper sizes. The custom
            // A4 dimensions use the same unit as PaperSize: 1/100 inch.
            printDocument.DefaultPageSettings.PaperSize = a4Paper ?? new PaperSize("A4", 827, 1169);
            printDocument.DefaultPageSettings.Margins = new Margins(
                A4MarginHundredthsInch,
                A4MarginHundredthsInch,
                A4MarginHundredthsInch,
                A4MarginHundredthsInch);
            printDocument.DefaultPageSettings.Landscape = a4Landscape;
        }

        private bool TryGetA4Orientation(RectangleF bounds, out bool landscape)
        {
            int columns;
            int rows;
            return TryGetA4Layout(bounds, out landscape, out columns, out rows);
        }

        private bool TryGetA4Layout(RectangleF bounds, out bool landscape, out int columns, out int rows)
        {
            const float gapMm = 5f;
            int requiredCount = Math.Max(1, designs.Count);
            float bestLayoutScore = float.MaxValue;
            landscape = false;
            columns = 0;
            rows = 0;

            for (int orientation = 0; orientation < 2; orientation++)
            {
                bool isLandscape = orientation == 1;
                SizeF pageSize = GetA4PageSize(isLandscape);
                float marginMm = A4MarginHundredthsInch * MillimetresPerHundredthInch;
                float availableWidth = pageSize.Width - marginMm * 2f;
                float availableHeight = pageSize.Height - marginMm * 2f;
                int maximumColumns = (int)Math.Floor((availableWidth + gapMm) / (bounds.Width + gapMm));
                int maximumRows = (int)Math.Floor((availableHeight + gapMm) / (bounds.Height + gapMm));
                if (maximumColumns < 1 || maximumRows < 1)
                {
                    continue;
                }

                for (int candidateColumns = 1; candidateColumns <= Math.Min(requiredCount, maximumColumns); candidateColumns++)
                {
                    int candidateRows = (int)Math.Ceiling((double)requiredCount / candidateColumns);
                    if (candidateRows > maximumRows)
                    {
                        continue;
                    }

                    float usedWidth = candidateColumns * bounds.Width + (candidateColumns - 1) * gapMm;
                    float usedHeight = candidateRows * bounds.Height + (candidateRows - 1) * gapMm;
                    float layoutAspect = usedWidth / usedHeight;
                    float pageAspect = availableWidth / availableHeight;
                    float layoutScore = Math.Abs((float)Math.Log(layoutAspect / pageAspect));
                    if (layoutScore < bestLayoutScore)
                    {
                        bestLayoutScore = layoutScore;
                        landscape = isLandscape;
                        columns = candidateColumns;
                        rows = candidateRows;
                    }
                }
            }

            return columns > 0;
        }

        private bool FitsOnA4(RectangleF bounds, bool landscape)
        {
            const float gapMm = 5f;
            SizeF pageSize = GetA4PageSize(landscape);
            float marginMm = A4MarginHundredthsInch * MillimetresPerHundredthInch;
            int columns = (int)Math.Floor((pageSize.Width - marginMm * 2f + gapMm) / (bounds.Width + gapMm));
            int rows = (int)Math.Floor((pageSize.Height - marginMm * 2f + gapMm) / (bounds.Height + gapMm));
            return columns * rows >= Math.Max(1, designs.Count);
        }

        private void UpdateMaximumNetCount()
        {
            float side;
            float faceHeight;
            float tolerance;
            if (!TryReadValues(out side, out faceHeight, out tolerance))
            {
                return;
            }

            int maximumNets = Math.Max(1, GetMaximumNetsOnA4(GetNetBounds(side, faceHeight, tolerance, radStandsOnVertex.Checked)));
            numHexagonCount.Maximum = maximumNets;
            UpdateDesignButtons();
        }

        private static int GetMaximumNetsOnA4(RectangleF bounds)
        {
            const float gapMm = 5f;
            float marginMm = A4MarginHundredthsInch * MillimetresPerHundredthInch;
            long maximumNets = 0;

            for (int orientation = 0; orientation < 2; orientation++)
            {
                SizeF pageSize = GetA4PageSize(orientation == 1);
                long columns = (long)Math.Floor((pageSize.Width - marginMm * 2f + gapMm) / (bounds.Width + gapMm));
                long rows = (long)Math.Floor((pageSize.Height - marginMm * 2f + gapMm) / (bounds.Height + gapMm));
                if (columns > 0 && rows > 0)
                {
                    maximumNets = Math.Max(maximumNets, columns * rows);
                }
            }

            return maximumNets > Int32.MaxValue ? Int32.MaxValue : (int)maximumNets;
        }

        private void UpdateA4Layout()
        {
            if (!hasNet)
            {
                return;
            }

            RectangleF bounds = GetNetBounds(sideLengthMm, lateralFaceHeightMm, faceToleranceMm, radStandsOnVertex.Checked);
            btnPrint.Enabled = TryGetA4Orientation(bounds, out a4Landscape);
            if (btnPrint.Enabled)
            {
                ConfigureA4Page();
            }
        }

        private static SizeF GetA4PageSize(bool landscape)
        {
            return landscape
                ? new SizeF(A4PortraitHeightMm, A4PortraitWidthMm)
                : new SizeF(A4PortraitWidthMm, A4PortraitHeightMm);
        }

        private static PointF[] GetHexagon(float side, bool standsOnVertex)
        {
            PointF[] points = new PointF[6];
            double startAngle = standsOnVertex ? -Math.PI / 2 : 0;
            for (int i = 0; i < points.Length; i++)
            {
                double angle = startAngle + i * Math.PI / 3;
                points[i] = new PointF((float)(Math.Cos(angle) * side), (float)(Math.Sin(angle) * side));
            }
            return points;
        }

        private static PointF GetOutwardNormal(PointF first, PointF second)
        {
            float dx = second.X - first.X;
            float dy = second.Y - first.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            PointF normal = new PointF(-dy / length, dx / length);
            PointF midpoint = new PointF((first.X + second.X) / 2f, (first.Y + second.Y) / 2f);
            if (normal.X * midpoint.X + normal.Y * midpoint.Y < 0)
            {
                normal = new PointF(-normal.X, -normal.Y);
            }
            return normal;
        }

        private static PointF[] ScalePoints(PointF[] points, float scale, PointF origin)
        {
            PointF[] result = new PointF[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                result[i] = new PointF(origin.X + points[i].X * scale, origin.Y + points[i].Y * scale);
            }
            return result;
        }

        private static RectangleF GetNetBounds(float side, float faceHeight, float tolerance, bool standsOnVertex)
        {
            PointF[] hexagon = GetHexagon(side, standsOnVertex);
            float left = float.MaxValue;
            float top = float.MaxValue;
            float right = float.MinValue;
            float bottom = float.MinValue;
            for (int i = 0; i < hexagon.Length; i++)
            {
                PointF first = hexagon[i];
                PointF second = hexagon[(i + 1) % hexagon.Length];
                PointF normal = GetOutwardNormal(first, second);
                AddBoundsPoint(first, ref left, ref top, ref right, ref bottom);
                AddBoundsPoint(second, ref left, ref top, ref right, ref bottom);
                if (faceHeight > 0)
                {
                    AddBoundsPoint(new PointF(first.X + normal.X * faceHeight, first.Y + normal.Y * faceHeight), ref left, ref top, ref right, ref bottom);
                    AddBoundsPoint(new PointF(second.X + normal.X * faceHeight, second.Y + normal.Y * faceHeight), ref left, ref top, ref right, ref bottom);
                    AddBoundsPoint(new PointF(first.X + normal.X * (faceHeight + tolerance), first.Y + normal.Y * (faceHeight + tolerance)), ref left, ref top, ref right, ref bottom);
                    AddBoundsPoint(new PointF(second.X + normal.X * (faceHeight + tolerance), second.Y + normal.Y * (faceHeight + tolerance)), ref left, ref top, ref right, ref bottom);
                }
            }
            return RectangleF.FromLTRB(left, top, right, bottom);
        }

        private static void AddBoundsPoint(PointF point, ref float left, ref float top, ref float right, ref float bottom)
        {
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }

        private bool TryReadValues(out float side, out float height, out float tolerance)
        {
            side = 0;
            height = 0;
            tolerance = 0;
            float selectedValue;
            if (!TryParsePositive(txtHexValue.Text, out selectedValue) || !TryParseNonNegative(txtFaceHeight.Text, out height))
            {
                return false;
            }

            if (height > 0 && !TryParseNonNegative(txtFaceTolerance.Text, out tolerance))
            {
                return false;
            }

            if (radVertexDiameter.Checked)
            {
                side = selectedValue / 2f;
            }
            else if (radEdgeDiameter.Checked)
            {
                side = selectedValue / Sqrt3;
            }
            else
            {
                side = selectedValue;
            }
            return side > 0;
        }

        private void UpdateConversionText()
        {
            float selectedValue;
            if (!TryParsePositive(txtHexValue.Text, out selectedValue))
            {
                lblConversions.Text = "Enter a positive hexagon measurement to see the equivalent dimensions.";
                return;
            }

            float side = radVertexDiameter.Checked ? selectedValue / 2f : radEdgeDiameter.Checked ? selectedValue / Sqrt3 : selectedValue;
            lblConversions.Text = "Equivalent dimensions:\r\n"
                + "Side length: " + Format(side) + " mm\r\n"
                + "Vertex to vertex: " + Format(side * 2f) + " mm\r\n"
                + "Edge to edge: " + Format(side * Sqrt3) + " mm";
        }

        private float GetSelectedMeasurement(float side)
        {
            if (radVertexDiameter.Checked)
            {
                return side * 2f;
            }
            if (radEdgeDiameter.Checked)
            {
                return side * Sqrt3;
            }
            return side;
        }

        private static bool TryParsePositive(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value > 0;
        }

        private static bool TryParseNonNegative(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value >= 0;
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }

        private sealed class NetPreviewPanel : Panel
        {
            public NetPreviewPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }
    }
}
