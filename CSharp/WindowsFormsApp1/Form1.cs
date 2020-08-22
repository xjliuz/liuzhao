using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using devDept.Eyeshot;
using devDept.Graphics;
using devDept.Geometry;
using devDept.Eyeshot.Entities;
using devDept.Eyeshot.Labels;
using System.Text;
using devDept.Eyeshot.Triangulation;
using devDept.Eyeshot.Translators;
using devDept.Serialization;
using devDept.Controls.WinForms;
using WindowsApplication1;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private bool _yAxisUp = false;

        public Form1()
        {
            InitializeComponent();
            model1.WorkCompleted += model1_WorkCompleted;
        }
        //导入事件
        private void importButton_Click(object sender, EventArgs e)
        {
            using (var importFileDialog1 = new OpenFileDialog())
            using (var importFileAddOn = new ImportFileAddOn())
            {
                string theFilter = "All compatible file types (*.*)|*.asc;*.stl;*.obj;*.las;*.3ds"
#if NURBS 
                               + ";*.igs;*.iges;*.stp;*.step"
#endif

#if SOLID
                               + ";*.ifc;*.ifczip"
#endif
                               + "|Points (*.asc)|*.asc|" + "WaveFront OBJ (*.obj)|*.obj|" + "Stereolithography (*.stl)|*.stl|" + "Laser LAS (*.las)|*.las|" + "3D Studio Max (*.3ds)|*.3ds";
#if NURBS
                theFilter += "|IGES (*.igs; *.iges)|*.igs; *.iges|" + "STEP (*.stp; *.step)|*.stp; *.step";
#endif

#if SOLID
                theFilter += "|IFC (*.ifc; *.ifczip)|*.ifc; *.ifczip";
#endif
                importFileDialog1.Filter = theFilter;

                importFileDialog1.Multiselect = false;
                importFileDialog1.AddExtension = true;
                importFileDialog1.CheckFileExists = true;
                importFileDialog1.CheckPathExists = true;

                if (importFileDialog1.ShowDialog(importFileAddOn, this) == DialogResult.OK)
                {
                    model1.Clear();
                    _yAxisUp = importFileAddOn.YAxisUp;

                    ReadFileAsync rfa = getReader(importFileDialog1.FileName);

                    if (rfa != null)
                    {
                        model1.StartWork(rfa);

                        model1.SetView(viewType.Trimetric, true, model1.AnimateCamera);

                        openButton.Enabled = false;
                        saveButton.Enabled = false;
                        importButton.Enabled = false;
                    }
                }
            }
        }
        
        //异步读取
        private ReadFileAsync getReader(string fileName)
        {
            string ext = System.IO.Path.GetExtension(fileName);

            if (ext != null)
            {
                ext = ext.TrimStart('.').ToLower();

                switch (ext)
                {
                    case "asc":
                        return new ReadASC(fileName);
                    case "stl":
                        return new ReadSTL(fileName);
                    case "obj":
                        return new ReadOBJ(fileName);
                    case "las":
                        return new ReadLAS(fileName);
                    case "3ds":
                        return new Read3DS(fileName);
#if NURBS
                    case "igs":
                    case "iges":
                        return new ReadIGES(fileName);
                    case "stp":
                    case "step":
                        return new ReadSTEP(fileName);
#endif
#if SOLID
                    case "ifc":
                    case "ifczip":
                        return new ReadIFC(fileName);
#endif
                }
            }

            return null;
        }

        //读取操作
        private void model1_WorkCompleted(object sender, devDept.Eyeshot.WorkCompletedEventArgs e)
        {
            if (e.WorkUnit is ReadFileAsync)
            {
                ReadFileAsync rfa = (ReadFileAsync)e.WorkUnit;
                rfa.AddToScene(model1);
                model1.SetView(viewType.Trimetric, true, model1.AnimateCamera);
            }
            EnableControls();
        }

        private void EnableControls()
        {
            importButton.Enabled = true;
            openButton.Enabled = true;
            saveButton.Enabled = true;
        }

        //打开eye文件
        private void openButton_Click(object sender, EventArgs e)
        {
            using (var openFileDialog1 = new OpenFileDialog())
            {
                openFileDialog1.Filter = "Eyeshot (*.eye)|*.eye";
                openFileDialog1.Multiselect = false;
                openFileDialog1.AddExtension = true;
                openFileDialog1.CheckFileExists = true;
                openFileDialog1.CheckPathExists = true;
                openFileDialog1.DereferenceLinks = true;

                _openFileAddOn = new OpenFileAddOn();
                _openFileAddOn.EventFileNameChanged += OpenFileAddOn_EventFileNameChanged;

                if (openFileDialog1.ShowDialog(_openFileAddOn, this) == DialogResult.OK)
                {
                    _yAxisUp = false;
                    model1.Clear();
                    ReadFile readFile = new ReadFile(openFileDialog1.FileName, (contentType)_openFileAddOn.ContentOption);
                    model1.StartWork(readFile);
                    model1.SetView(viewType.Trimetric, true, model1.AnimateCamera);
                    openButton.Enabled = false;
                }

                _openFileAddOn.EventFileNameChanged -= OpenFileAddOn_EventFileNameChanged;
                _openFileAddOn.Dispose();
                _openFileAddOn = null;
            }
        }

        private OpenFileAddOn _openFileAddOn;

        private void OpenFileAddOn_EventFileNameChanged(IWin32Window sender, string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                ReadFile rf = new ReadFile(filePath, true);
                _openFileAddOn.SetFileInfo(rf.GetThumbnail(), rf.GetFileInfo());
            }
            else
            {
                _openFileAddOn.ResetFileInfo();
            }
        }

        //保存事件
        private void saveButton_Click(object sender, EventArgs e)
        {
            using (var saveFileDialog = new SaveFileDialog())
            using (var saveFileAddOn = new SaveFileAddOn())
            {
                saveFileDialog.Filter = "Eyeshot (*.eye)|*.eye";
                saveFileDialog.AddExtension = true;
                saveFileDialog.CheckPathExists = true;

                if (saveFileDialog.ShowDialog(saveFileAddOn, this) == DialogResult.OK)
                {
                    WriteFile writeFile = new WriteFile(new WriteFileParams(model1) { Content = (contentType)saveFileAddOn.ContentOption, SerializationMode = (serializationType)saveFileAddOn.SerialOption, SelectedOnly = saveFileAddOn.SelectedOnly, Purge = saveFileAddOn.Purge }, saveFileDialog.FileName);
                    model1.StartWork(writeFile);
                    openButton.Enabled = false;
                    saveButton.Enabled = false;
                    importButton.Enabled = false;
                }
            }
        }


        //导出事件
        private void exportButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            string theFilter = "WaveFront OBJ (*.obj)|*.obj|" + "Stereolithography (*.stl)|*.stl|" + "Laser LAS (*.las)|*.las|" + "WebGL (*.html)|*.html";
#if NURBS
            theFilter += "|STandard for the Exchange of Product (*.step)|*.step|" + "Initial Graphics Exchange Specification (*.iges)|*.iges";
#endif
            saveFileDialog1.Filter = theFilter;

            saveFileDialog1.AddExtension = true;
            saveFileDialog1.CheckPathExists = true;

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                WriteFileAsync wfa = null;
                WriteParams dataParams;

                switch (saveFileDialog1.FilterIndex)
                {
                    case 1:
                        dataParams = new WriteParamsWithMaterials(model1);
                        wfa = new WriteOBJ((WriteParamsWithMaterials)dataParams, saveFileDialog1.FileName);
                        break;

                    case 2:
                        dataParams = new WriteParams(model1);
                        wfa = new WriteSTL(dataParams, saveFileDialog1.FileName);
                        break;
                    case 3:
                        dataParams = null;
                        wfa = new WriteLAS(model1.Entities.Where(x => x is FastPointCloud).FirstOrDefault() as FastPointCloud, saveFileDialog1.FileName);
                        break;
                    case 4:
                        dataParams = new WriteParamsWithMaterials(model1);
                        wfa = new WriteWebGL((WriteParamsWithMaterials)dataParams, model1.DefaultMaterial, saveFileDialog1.FileName);
                        break;
#if NURBS
                    case 5:
                        dataParams = new WriteParamsWithUnits(model1);
                        wfa = new WriteSTEP((WriteParamsWithUnits)dataParams, saveFileDialog1.FileName);
                        break;

                    case 6:
                        dataParams = new WriteParamsWithUnits(model1);
                        wfa = new WriteIGES((WriteParamsWithUnits)dataParams, saveFileDialog1.FileName);
                        break;
#endif
                }

                model1.StartWork(wfa);
            }
        }

        // wire shade rendered hidden 均为视图转换功能
        private void wireframeButton_CheckedChanged(object sender, EventArgs e)
        {
            SetDisplayMode(model1, displayType.Wireframe);
        }

        private void shadedButton_CheckedChanged(object sender, EventArgs e)
        {
            SetDisplayMode(model1, displayType.Shaded);
        }

        private void renderedButton_CheckedChanged(object sender, EventArgs e)
        {
            SetDisplayMode(model1, displayType.Rendered);
        }

        private void hiddenLinesButton_CheckedChanged(object sender, EventArgs e)
        {
            SetDisplayMode(model1, displayType.HiddenLines);
        }

        public static void SetDisplayMode(Model model, displayType displayType)
        {
            model.DisplayMode = displayType;
            SetBackgroundStyleAndColor(model);
            model.Entities.UpdateBoundingBox(); // Updates simplified representation (when available)
            model.Invalidate();
        }

        public static void SetBackgroundStyleAndColor(Model model)
        {
            model.CoordinateSystemIcon.Lighting = false;
            model.ViewCubeIcon.Lighting = false;

            switch (model.DisplayMode)
            {

                case displayType.HiddenLines:
                    model.Background.TopColor = Color.FromArgb(0xD2, 0xD0, 0xB9);

                    model.CoordinateSystemIcon.Lighting = true;
                    model.ViewCubeIcon.Lighting = true;

                    break;

                default:
                    model.Background.TopColor = Color.Snow;
                    break;
            }

            model.CompileUserInterfaceElements();
        }

        //求图形的表面积
        private void areaButton_Click(object sender, EventArgs e)
        {

            AreaProperties ap = new AreaProperties();

            int count = 0;

            var blockReferenceNotScaled = true;
            for (int i = 0; i < model1.Entities.Count && blockReferenceNotScaled; i++)
            {

                Entity ent = model1.Entities[i];

                count += AddAreaProperty(ap, ent, out blockReferenceNotScaled);
            }

            StringBuilder text = new StringBuilder();

            if (blockReferenceNotScaled)
            {
                text.AppendLine(count + " entity(ies) selected");
                text.AppendLine("---------------------");

                if (ap.Centroid != null)
                {

                    double x, y, z;
                    double xx, yy, zz, xy, zx, yz;
                    MomentOfInertia world, centroid;

                    ap.GetResults(ap.Area, ap.Centroid, out x, out y, out z, out xx, out yy, out zz, out xy, out zx, out yz, out world, out centroid);

                    text.AppendLine("Cumulative area: " + ap.Area + " square " + model1.Units.ToString().ToLower());
                     }
            }
            else
            {
                text.AppendLine("Error: scaled BlockReference is not supported.");

                text.AppendLine("---------------------");
            }

            DetailsForm rf = new DetailsForm();

            rf.Text = "Area Properties";

            rf.contentTextBox.Text = text.ToString();

            rf.Show();
        }

        //求体积
        private void volumeButton_Click(object sender, EventArgs e)
        {
            VolumeProperties vp = new VolumeProperties();

            int count = 0;
            var blockReferenceNotScaled = true;

            for (int i = 0; i < model1.Entities.Count && blockReferenceNotScaled; i++)
            {
                Entity ent = model1.Entities[i];

                count += AddVolumeProperty(vp, ent, out blockReferenceNotScaled);
            }

            StringBuilder text = new StringBuilder();

            if (blockReferenceNotScaled)
            {
                text.AppendLine(count + " entity(ies) selected");
                text.AppendLine("---------------------");

                if (vp.Centroid != null)
                {

                    double x, y, z;
                    double xx, yy, zz, xy, zx, yz;
                    MomentOfInertia world, centroid;

                    vp.GetResults(vp.Volume, vp.Centroid, out x, out y, out z, out xx, out yy, out zz, out xy, out zx, out yz, out world, out centroid);

                    text.AppendLine("Cumulative volume: " + vp.Volume + " cubic " + model1.Units.ToString().ToLower());
                    text.AppendLine("Cumulative centroid: " + vp.Centroid);
                }
            }
            else
            {
                text.AppendLine("Error: scaled BlockReference not supported.");
                text.AppendLine("---------------------");
            }

            DetailsForm rf = new DetailsForm();

            rf.Text = "Volume Properties";

            rf.contentTextBox.Text = text.ToString();

            rf.Show();
        }

        private int AddAreaProperty(AreaProperties ap, Entity ent, out bool blockReferenceNotScaled, bool isParentSelected = false)
        {
            int count = 0;
            blockReferenceNotScaled = true;

            if (ent.Selected || isParentSelected)
            {
                if (ent is IFace)
                {
                    IFace itfFace = (IFace)ent;

                    Mesh[] meshes = itfFace.GetPolygonMeshes();

                    foreach (Mesh mesh in meshes)
                    {
                        ap.Add(mesh.Vertices, mesh.Triangles);
                    }
                    count++;
                }
                else if (ent is BlockReference)
                {
                    var br = (BlockReference)ent;

                    if (br.GetScaleFactorX() != 1 &&
                         br.GetScaleFactorY() != 1 &&
                         br.GetScaleFactorZ() != 1)
                    {
                        blockReferenceNotScaled = false;
                        return count;
                    }

                    foreach (var e in br.GetEntities(model1.Blocks))
                    {
                        count += AddAreaProperty(ap, e, out blockReferenceNotScaled, true);

                        if (!blockReferenceNotScaled) return count;
                    }
                }
                else
                {
                    ICurve itfCurve = (ICurve)ent;

                    if (itfCurve.IsClosed)
                        ap.Add(ent.Vertices);

                    count++;
                }

            }
#if NURBS
            else if (ent is Brep)
            {
                Brep brep = (Brep)ent;

                for (int j = 0; j < brep.Faces.Length; j++)
                {
                    Brep.Face sf = brep.Faces[j];
                    Mesh[] faceTessellation = sf.Tessellation;

                    if (brep.GetFaceSelection(j))
                    {
                        foreach (Mesh m in faceTessellation)
                            ap.Add(m.Vertices, m.Triangles);

                        count++;
                    }
                }
            }
#endif
            return count;
        }

        private int AddVolumeProperty(VolumeProperties vp, Entity ent, out bool blockReferenceNotScaled, bool isParentSelected = false)
        {
            int count = 0;
            blockReferenceNotScaled = true;

            if (ent.Selected || isParentSelected)
            {
                if (ent is IFace)
                {
                    IFace itfFace = (IFace)ent;

                    Mesh[] meshes = itfFace.GetPolygonMeshes();

                    foreach (Mesh mesh in meshes)
                    {
                        vp.Add(mesh.Vertices, mesh.Triangles);
                    }
                    count++;
                }
                else if (ent is BlockReference)
                {
                    var br = (BlockReference)ent;

                    if (br.GetScaleFactorX() != 1 &&
                         br.GetScaleFactorY() != 1 &&
                         br.GetScaleFactorZ() != 1)
                    {
                        blockReferenceNotScaled = false;
                        return count;
                    }

                    foreach (var e in br.GetEntities(model1.Blocks))
                    {
                        count += AddVolumeProperty(vp, e, out blockReferenceNotScaled, true);

                        if (!blockReferenceNotScaled) return count;
                    }
                }

            }

            return count;
        }

        //图形上显示长宽高
        private void showExtentsButton_CheckedChanged(object sender, EventArgs e)
        {
            model1.BoundingBox.Visible = showExtentsButton.Checked;
            model1.Invalidate();
        }

        //打印
        private void printButton_Click(object sender, EventArgs e)
        {
            model1.Print();
        }

        //打印预览
        private void printPreviewButton_Click(object sender, EventArgs e)
        {
            model1.PrintPreview(new Size(500, 400));
        }

        //选中目标获得数据
        private void invertSelectionButton_Click(object sender, EventArgs e)
        {
            if (model1.ActionMode == actionType.SelectVisibleByPickLabel)

                model1.Viewports[0].Labels.InvertSelection();

            else

                model1.Entities.InvertSelection();

            model1.Invalidate();
        }
    }
}
