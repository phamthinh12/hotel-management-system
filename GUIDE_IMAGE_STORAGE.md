# 📸 Hướng Dẫn Lưu Ảnh Không Vào Database

Khi muốn lưu ảnh đặt phòng (ảnh chứng minh thân phận, ảnh phòng, etc.), bạn có 3 lựa chọn chính:

---

## **Cách 1: Lưu vào Local Folder** ⭐ (Đơn giản, phù hợp phát triển)

### A. Cấu hình Model
Thêm property để lưu đường dẫn ảnh:

```csharp
// Models/DatPhong.cs
public class DatPhong
{
    public string MaDatPhong { get; set; }
    public string? MaKhachHang { get; set; }
    public DateTime? NgayDat { get; set; }
    public DateTime? NgayNhanDuKien { get; set; }
    public DateTime? NgayTraDuKien { get; set; }
    public string? TrangThai { get; set; }
    
    // Thêm đây - đường dẫn ảnh
    public string? AnhDaiDien { get; set; }  // Ảnh đại diện đặt phòng
    
    public virtual KhachHang? MaKhachHangNavigation { get; set; }
    public virtual ICollection<CtdatPhong> CtdatPhongs { get; set; }
}
```

### B. Tạo Migration
```powershell
# Chạy trong Package Manager Console
Add-Migration AddImagePathToDatPhong
Update-Database
```

### C. Cập nhật Controller
Sửa method `CreateModern` POST:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(
    [Bind("MaDatPhong,MaKhachHang,NgayDat,NgayNhanDuKien,NgayTraDuKien")] 
    DatPhong datPhong, 
    string MaLoaiPhong, 
    int SoLuong, 
    string GhiChu,
    IFormFile? AnhFile)  // Thêm parameter ảnh
{
    try
    {
        // Validate customer & room type...
        var khachHang = await _context.KhachHangs.FindAsync(datPhong.MaKhachHang);
        if (khachHang == null)
        {
            ViewBag.ErrorMessage = "Khách hàng không tồn tại";
            return RedirectToAction(nameof(CreateModern));
        }

        var loaiPhong = await _context.LoaiPhongs.FindAsync(MaLoaiPhong);
        if (loaiPhong == null)
        {
            ViewBag.ErrorMessage = "Loại phòng không tồn tại";
            return RedirectToAction(nameof(CreateModern));
        }

        // **✨ Xử lý upload ảnh**
        if (AnhFile != null && AnhFile.Length > 0)
        {
            // Kiểm tra file là ảnh
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var fileExtension = Path.GetExtension(AnhFile.FileName).ToLower();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                ViewBag.ErrorMessage = "Chỉ chấp nhận file ảnh (.jpg, .png, .gif)";
                return RedirectToAction(nameof(CreateModern));
            }

            // Kiểm tra kích thước (max 5MB)
            if (AnhFile.Length > 5 * 1024 * 1024)
            {
                ViewBag.ErrorMessage = "Ảnh không được vượt quá 5MB";
                return RedirectToAction(nameof(CreateModern));
            }

            // Tạo folder nếu chưa tồn tại
            string uploadPath = Path.Combine(
                Directory.GetCurrentDirectory(), 
                "wwwroot", 
                "uploads", 
                "datphong"
            );
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            // Tạo tên file unique
            string fileName = $"{Guid.NewGuid()}{fileExtension}";
            string filePath = Path.Combine(uploadPath, fileName);

            // Lưu file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await AnhFile.CopyToAsync(fileStream);
            }

            // Lưu đường dẫn vào model (không lưu file vào DB)
            datPhong.AnhDaiDien = $"/uploads/datphong/{fileName}";

            _logger.LogInformation("Upload ảnh thành công: {0}", fileName);
        }

        // Generate booking ID...
        datPhong.MaDatPhong = GenerateMaDatPhong();
        datPhong.NgayDat = DateTime.Now;
        datPhong.TrangThai = "Chờ xác nhận";

        // Lưu vào DB
        _context.Add(datPhong);
        await _context.SaveChangesAsync();

        // Add booking details...
        var giaDatPhong = await _context.GiaPhongs
            .Where(gp => gp.MaLoaiPhong == MaLoaiPhong && 
                        gp.NgayBatDau <= DateTime.Now && 
                        gp.NgayKetThuc >= DateTime.Now)
            .FirstOrDefaultAsync();

        double giaTinh = giaDatPhong?.Gia ?? 0.0;
        int soNgay = (int)(((datPhong.NgayTraDuKien ?? DateTime.Now) - 
                           (datPhong.NgayNhanDuKien ?? DateTime.Now)).TotalDays);

        for (int i = 0; i < SoLuong; i++)
        {
            var ctdatPhong = new CtdatPhong
            {
                MaDatPhong = datPhong.MaDatPhong,
                MaLoaiPhong = MaLoaiPhong,
                SoLuong = 1,
                GiaTamTinh = giaTinh * soNgay
            };
            _context.Add(ctdatPhong);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Người dùng {0} tạo đơn đặt phòng: {1}", 
            User.Identity?.Name, datPhong.MaDatPhong);

        return RedirectToAction(nameof(Details), new { id = datPhong.MaDatPhong });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Lỗi khi tạo đơn đặt phòng");
        ViewBag.ErrorMessage = "Có lỗi xảy ra khi tạo đơn đặt phòng";
        return RedirectToAction(nameof(CreateModern));
    }
}
```

### D. Cập nhật View
Thêm input file vào form:

```html
<!-- Trong Views/DatPhong/CreateModern.cshtml, thêm vào confirmSection -->
<div class="form-section" id="confirmSection" style="display: none;">
    <div class="section-title">
        <div class="section-icon">
            <span class="material-icons">check_circle</span>
        </div>
        Xác Nhận Đặt Phòng
    </div>

    <!-- ... existing content ... -->

    <div class="form-group">
        <label class="form-label">📸 Ảnh Đặt Phòng (Tuỳ chọn)</label>
        <input type="file" name="AnhFile" class="form-control" accept="image/*"/>
        <small style="color: var(--on-surface-variant);">JPG, PNG, GIF - Max 5MB</small>
    </div>

    <div class="form-group">
        <label class="form-label">Ghi Chú Thêm</label>
        <textarea class="form-control" name="GhiChu" rows="3"></textarea>
    </div>

    <button type="submit" class="btn-primary-custom w-100">
        <span class="material-icons">done_all</span>
        Tạo Đơn Đặt Phòng
    </button>
</div>
```

### E. Hiển thị ảnh trong Details view
```html
<!-- Views/DatPhong/Details.cshtml -->
@{
    var datPhong = (WebKhachSan.Models.DatPhong)Model;
}

@if (!string.IsNullOrEmpty(datPhong.AnhDaiDien))
{
    <div style="margin-bottom: 1rem;">
        <img src="@datPhong.AnhDaiDien" 
             alt="Ảnh đặt phòng" 
             style="max-width: 300px; border-radius: 0.5rem;"/>
    </div>
}
```

---

## **Cách 2: Lưu vào Azure Blob Storage** ☁️ (Sản phẩm, An toàn)

### Setup

**1. Cài NuGet Package:**
```powershell
Install-Package Azure.Storage.Blobs
```

**2. Thêm vào `appsettings.json`:**
```json
{
  "AzureStorageConnection": "DefaultEndpointsProtocol=https;AccountName=yourname;AccountKey=yourkey;EndpointSuffix=core.windows.net"
}
```

**3. Tạo Service:**
```csharp
// Services/ImageUploadService.cs
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

public class ImageUploadService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<ImageUploadService> _logger;

    public ImageUploadService(IConfiguration config, ILogger<ImageUploadService> logger)
    {
        _logger = logger;
        var connectionString = config["AzureStorageConnection"];
        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient("datphong-images");
    }

    public async Task<string> UploadImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return null;

        try
        {
            string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            BlobClient blobClient = _containerClient.GetBlobClient(fileName);

            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, overwrite: true);
            }

            _logger.LogInformation("Uploaded image: {0}", fileName);
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            throw;
        }
    }
}
```

**4. Đăng ký trong `Program.cs`:**
```csharp
builder.Services.AddScoped<ImageUploadService>();
```

---

## **Cách 3: Lưu vào AWS S3** (Nếu dùng AWS)

Thay `Azure.Storage.Blobs` bằng:
```powershell
Install-Package AWSSDK.S3
```

---

## **⚠️ Lưu ý Bảo Mật**

1. **Kiểm tra file:**
   - ✅ Kiểm tra extension
   - ✅ Kiểm tra MIME type
   - ✅ Kiểm tra kích thước
   - ❌ Không tin `FileName` từ user

2. **Tao tên file unique:**
   ```csharp
   string fileName = $"{Guid.NewGuid()}_{DateTime.Now:yyyyMMddHHmmss}_{Path.GetRandomFileName()}";
   ```

3. **Cleanup:**
   - Thêm chức năng xóa ảnh cũ khi xóa đơn đặt phòng
   - Chạy job xóa ảnh orphan hàng tuần

---

## **So Sánh Các Phương Pháp**

| Phương Pháp | Ưu Điểm | Nhược Điểm | Chi Phí |
|---|---|---|---|
| **Local Folder** | 📁 Đơn giản, nhanh | Khó backup, restart mất | Miễn phí |
| **Azure Blob** | ☁️ An toàn, CDN, backup tự động | Phức tạp hơn | ~$0.025/GB |
| **AWS S3** | ☁️ Mạnh, CDN, scale tốt | Phải học API AWS | ~$0.023/GB |
| **Database** | 📊 Tập trung | **⚠️ Siêu chậm, database nặng** | - |

---

## **Khuyến Nghị**
- **Phát triển/Demo:** Local Folder (Cách 1)
- **Production:** Azure Blob hoặc AWS S3 (Cách 2/3)
- **❌ Tuyệt đối không dùng:** Lưu ảnh vào Database
