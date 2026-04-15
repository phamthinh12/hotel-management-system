using System.ComponentModel.DataAnnotations;
using WebKhachSan.Models;

namespace WebKhachSan.ViewModels
{
    public class DanhSachPhongTrongViewModel
    {
        public string? MaPhong { get; set; }
        public string? SoPhong { get; set; }
        public string? MaLoaiPhong { get; set; }
        public List<LoaiPhong> LoaiPhongs { get; set; } = new();
        public List<PhongTrongItemViewModel> Phongs { get; set; } = new();
    }

    public class PhongTrongItemViewModel
    {
        public string MaPhong { get; set; } = string.Empty;
        public string? SoPhong { get; set; }
        public string? MaLoaiPhong { get; set; }
        public string? TenLoaiPhong { get; set; }
        public int? SoNguoiToiDa { get; set; }
        public double? GiaHienTai { get; set; }
    }

    public class ThuePhongOfflineViewModel
    {
        [Required]
        public string MaPhong { get; set; } = string.Empty;

        public string? SoPhong { get; set; }
        public string? TenLoaiPhong { get; set; }
        public double? GiaHienTai { get; set; }

        [Required(ErrorMessage = "Vui long nhap ten khach hang.")]
        [StringLength(100, ErrorMessage = "Ten khach hang khong duoc vuot qua 100 ky tu.")]
        public string? TenKhachHang { get; set; }

        [Required(ErrorMessage = "Vui long nhap dien thoai.")]
        [StringLength(15, ErrorMessage = "Dien thoai khong duoc vuot qua 15 ky tu.")]
        public string? DienThoai { get; set; }

        [StringLength(255, ErrorMessage = "Dia chi khong duoc vuot qua 255 ky tu.")]
        public string? DiaChi { get; set; }

        [StringLength(12, ErrorMessage = "CCCD khong duoc vuot qua 12 so.")]
        [RegularExpression(@"^\d*$", ErrorMessage = "CCCD chi duoc chua chu so.")]
        public string? Cccd { get; set; }

        [Required(ErrorMessage = "Vui long chon ngay nhan phong.")]
        [DataType(DataType.Date)]
        public DateTime? NgayNhan { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime? NgayTra { get; set; }
    }

    public class TraPhongViewModel
    {
        public string MaThuePhong { get; set; } = string.Empty;
        public string? TenKhachHang { get; set; }
        public string? SoPhong { get; set; }
        public DateTime? NgayNhan { get; set; }

        [Required(ErrorMessage = "Vui long chon ngay tra phong.")]
        [DataType(DataType.Date)]
        public DateTime NgayTra { get; set; } = DateTime.Today;
    }

    public class ThuePhongStep1ViewModel
    {
        [Required(ErrorMessage = "Vui long nhap ten khach hang.")]
        [StringLength(100, ErrorMessage = "Ten khach hang khong duoc vuot qua 100 ky tu.")]
        public string TenKhachHang { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui long nhap dien thoai.")]
        [StringLength(15, ErrorMessage = "Dien thoai khong duoc vuot qua 15 ky tu.")]
        public string DienThoai { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Dia chi khong duoc vuot qua 255 ky tu.")]
        public string? DiaChi { get; set; }

        [StringLength(12, ErrorMessage = "CCCD khong duoc vuot qua 12 so.")]
        [RegularExpression(@"^\d*$", ErrorMessage = "CCCD chi duoc chua chu so.")]
        public string? Cccd { get; set; }
    }

    public class ThuePhongStep2ViewModel
    {
        public string TenKhachHang { get; set; } = string.Empty;
        public string? DienThoai { get; set; }
        public string? DiaChi { get; set; }
        public string? Cccd { get; set; }
        public DateTime? NgayNhan { get; set; }
        public List<PhongTrongItemViewModel> Phongs { get; set; } = new();
        public List<string> SelectedPhongs { get; set; } = new();
    }

    public class ThuePhongConfirmViewModel
    {
        public string? Cccd { get; set; }
        public string TenKhachHang { get; set; } = string.Empty;
        public string? DienThoai { get; set; }
        public string? DiaChi { get; set; }
        public DateTime NgayNhan { get; set; }
        public List<PhongChonViewModel> PhongsChon { get; set; } = new();
        public double TongTien { get; set; }
    }

    public class PhongChonViewModel
    {
        public string MaPhong { get; set; } = string.Empty;
        public string? SoPhong { get; set; }
        public string? TenLoaiPhong { get; set; }
        public double? GiaThue { get; set; }
    }
}
