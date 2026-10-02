using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Caelus.Core;
using Caelus.Services;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace Caelus.UI;

public partial class CustomSkyWindow : Window
{
    private static readonly (string Id, string Label)[] FaceOrder =
    {
        ("ft", "Front (ft)"),
        ("bk", "Back (bk)"),
        ("lf", "Left (lf)"),
        ("rt", "Right (rt)"),
        ("up", "Top (up)"),
        ("dn", "Bottom (dn)")
    };

    private readonly Dictionary<string, string?> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Border> _slots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DiffuseMaterial> _materials = new(StringComparer.OrdinalIgnoreCase);

    private bool _dragging;
    private System.Windows.Point _last;
    private double _yaw = 20;
    private double _pitch = -8;
    private bool _applied;

    public CustomSkyWindow()
    {
        InitializeComponent();
        foreach (var (id, _) in FaceOrder)
            _paths[id] = null;

        BuildSlots();
        BuildSkyCube();
        LoadExistingCustom();
        UpdateCamera();
        UpdateApplyEnabled();
        StatusText.Text = "Pick all six faces to enable Apply.";
    }

    /// <summary>True when the user applied a sky successfully.</summary>
    public bool Applied => _applied;

    private void BuildSlots()
    {
        FaceGrid.Children.Clear();
        var modern = ThemeService.IsModern;
        foreach (var (id, label) in FaceOrder)
        {
            var thumb = new Border
            {
                Height = 72,
                CornerRadius = new CornerRadius(modern ? 8 : 3),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 0, 4)
            };
            thumb.SetResourceReference(Border.BackgroundProperty, "InputBrush");

            var caption = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            var hint = new TextBlock
            {
                Text = "Click to choose",
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");

            var stack = new StackPanel { Margin = new Thickness(0, 0, 8, 10) };
            stack.Children.Add(thumb);
            stack.Children.Add(caption);
            stack.Children.Add(hint);

            var card = new Border
            {
                Padding = new Thickness(6),
                CornerRadius = new CornerRadius(modern ? 10 : 4),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = id,
                Child = stack,
                Margin = new Thickness(0, 0, 6, 0)
            };
            card.SetResourceReference(Border.BackgroundProperty, "CardBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            card.MouseLeftButtonUp += (_, _) => PickFace(id);

            _slots[id] = thumb;
            FaceGrid.Children.Add(card);
        }
    }

    private void BuildSkyCube()
    {
        var group = new Model3DGroup();
        // Inside of a unit cube. Positions match SkyboxService.Direction / Roblox sky seams.
        AddFace(group, "ft",
            new Point3D(-1, -1, 1), new Point3D(1, -1, 1), new Point3D(1, 1, 1), new Point3D(-1, 1, 1));
        AddFace(group, "bk",
            new Point3D(1, -1, -1), new Point3D(-1, -1, -1), new Point3D(-1, 1, -1), new Point3D(1, 1, -1));
        AddFace(group, "lf",
            new Point3D(1, -1, 1), new Point3D(1, -1, -1), new Point3D(1, 1, -1), new Point3D(1, 1, 1));
        AddFace(group, "rt",
            new Point3D(-1, -1, -1), new Point3D(-1, -1, 1), new Point3D(-1, 1, 1), new Point3D(-1, 1, -1));
        AddFace(group, "up",
            new Point3D(-1, 1, 1), new Point3D(1, 1, 1), new Point3D(1, 1, -1), new Point3D(-1, 1, -1));
        AddFace(group, "dn",
            new Point3D(-1, -1, -1), new Point3D(1, -1, -1), new Point3D(1, -1, 1), new Point3D(-1, -1, 1));

        SkyModel.Content = group;
        SetPlaceholderMaterials();
    }

    private void AddFace(Model3DGroup group, string face, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
    {
        var mesh = new MeshGeometry3D();
        mesh.Positions.Add(p0);
        mesh.Positions.Add(p1);
        mesh.Positions.Add(p2);
        mesh.Positions.Add(p3);
        // Two triangles, winding so the texture faces inward (camera at origin).
        mesh.TriangleIndices.Add(0);
        mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(1);
        mesh.TriangleIndices.Add(0);
        mesh.TriangleIndices.Add(3);
        mesh.TriangleIndices.Add(2);
        mesh.TextureCoordinates.Add(new System.Windows.Point(0, 1));
        mesh.TextureCoordinates.Add(new System.Windows.Point(1, 1));
        mesh.TextureCoordinates.Add(new System.Windows.Point(1, 0));
        mesh.TextureCoordinates.Add(new System.Windows.Point(0, 0));

        var material = new DiffuseMaterial(PlaceholderBrush());
        _materials[face] = material;
        group.Children.Add(new GeometryModel3D(mesh, material));
    }

    private static MediaBrush PlaceholderBrush()
    {
        var brush = new SolidColorBrush(MediaColor.FromRgb(0x2A, 0x2A, 0x30));
        brush.Freeze();
        return brush;
    }

    private void SetPlaceholderMaterials()
    {
        foreach (var material in _materials.Values)
            material.Brush = PlaceholderBrush();
    }

    private void LoadExistingCustom()
    {
        if (SkyboxService.CurrentId() != SkyboxService.CustomId)
            return;

        foreach (var (id, _) in FaceOrder)
        {
            var bitmap = SkyboxService.FacePreview(SkyboxService.CustomId, id, 256);
            if (bitmap is null)
                continue;
            SetSlotPreview(id, bitmap);
            SetFaceMaterial(id, bitmap);
        }

        StatusText.Text = "Showing your current custom sky. Replace any face, then Apply.";
        UpdateApplyEnabled();
    }

    private void PickFace(string face)
    {
        using var dialog = new OpenFileDialog
        {
            Title = $"Choose the {FaceOrder.First(f => f.Id == face).Label} image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        SetFacePath(face, dialog.FileName);
    }

    private void ImportSix_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose six sky images (names ending in _bk, _dn, _ft, _lf, _rt, _up)",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var matched = SkyboxService.MatchFaces(dialog.FileNames, out var unmatched);
        foreach (var (face, path) in matched)
            SetFacePath(face, path);

        var missing = SkyboxService.Faces.Count(face => string.IsNullOrWhiteSpace(_paths[face]));
        StatusText.Text = missing == 0
            ? "All six faces set."
            : $"Matched {matched.Count}. Still need {missing} face(s)" +
              (unmatched.Count > 0 ? $"; {unmatched.Count} file(s) didn't match a face name." : ".");
        UpdateApplyEnabled();
    }

    private void SetFacePath(string face, string path)
    {
        _paths[face] = path;
        var bitmap = SkyboxService.LoadImageSource(path, 512);
        if (bitmap is null)
        {
            StatusText.Text = $"Could not read {Path.GetFileName(path)}.";
            return;
        }

        SetSlotPreview(face, bitmap);
        SetFaceMaterial(face, bitmap);
        UpdateApplyEnabled();
        StatusText.Text = $"{FaceOrder.First(f => f.Id == face).Label}: {Path.GetFileName(path)}";
    }

    private void SetSlotPreview(string face, ImageSource bitmap)
    {
        if (!_slots.TryGetValue(face, out var slot))
            return;
        slot.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
    }

    private void SetFaceMaterial(string face, ImageSource bitmap)
    {
        if (!_materials.TryGetValue(face, out var material))
            return;
        var brush = new ImageBrush(bitmap)
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.None,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 1, 1)
        };
        brush.Freeze();
        material.Brush = brush;
    }

    private void UpdateApplyEnabled()
    {
        ApplyButton.IsEnabled = SkyboxService.Faces.All(face => !string.IsNullOrWhiteSpace(_paths[face]));
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        var images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var face in SkyboxService.Faces)
        {
            if (string.IsNullOrWhiteSpace(_paths[face]))
            {
                System.Windows.MessageBox.Show(
                    $"Still need an image for {FaceOrder.First(f => f.Id == face).Label}.",
                    AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            images[face] = _paths[face]!;
        }

        ApplyButton.IsEnabled = false;
        StatusText.Text = "Converting your images…";
        try
        {
            var loaded = SkyboxService.LoadCustom(images);
            var description = string.Join(", ", SkyboxService.Faces.Select(face => Path.GetFileName(images[face])));
            await Task.Run(() => SkyboxService.ApplyCustom(loaded, description));
            _applied = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Logger.Error("Sky", ex);
            StatusText.Text = $"Could not apply the sky: {ex.Message}";
            System.Windows.MessageBox.Show($"Could not apply the sky.\n\n{ex.Message}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateApplyEnabled();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SkyView_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _last = e.GetPosition(SkyView);
        SkyView.CaptureMouse();
        PreviewHint.Visibility = Visibility.Collapsed;
    }

    private void SkyView_MouseUp(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _dragging = false;
        SkyView.ReleaseMouseCapture();
    }

    private void SkyView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging)
            return;

        var pos = e.GetPosition(SkyView);
        _yaw += (pos.X - _last.X) * 0.35;
        _pitch += (pos.Y - _last.Y) * 0.35;
        _pitch = Math.Clamp(_pitch, -89, 89);
        _last = pos;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        var yaw = _yaw * Math.PI / 180;
        var pitch = _pitch * Math.PI / 180;
        var x = Math.Cos(pitch) * Math.Sin(yaw);
        var y = Math.Sin(pitch);
        var z = -Math.Cos(pitch) * Math.Cos(yaw);
        Camera.LookDirection = new Vector3D(x, y, z);
        Camera.UpDirection = new Vector3D(0, 1, 0);
    }
}
