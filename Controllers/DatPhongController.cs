using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebKhachSan.Models;
using System.Text.Json.Serialization;

namespace WebKhachSan.Controllers
{
    [Authorize]
    public class DatPhongController : Controller
    {
        private readonly QuanLyKhachSanContext _context;
        private readonly ILogger<DatPhongController> _logger;

        public DatPhongController(QuanLyKhachSanContext context, ILogger<DatPhongController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: DatPhong
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Người dùng {0} xem danh sách đặt phòng", User.Identity?.Name);
            
            var datPhongs = await _context.DatPhongs
                .Include(dp => dp.MaKhachHangNavigation)
                .Include(dp => dp.CtdatPhongs)
                    .ThenInclude(ct => ct.MaLoaiPhongNavigation)
                .OrderByDescending(dp => dp.NgayDat)
                .ToListAsync();

            return View(datPhongs);
        }

        // GET: DatPhong/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var datPhong = await _context.DatPhongs
                .Include(dp => dp.MaKhachHangNavigation)
                .Include(dp => dp.CtdatPhongs)
                    .ThenInclude(ct => ct.MaLoaiPhongNavigation)
                .FirstOrDefaultAsync(m => m.MaDatPhong == id);

            if (datPhong == null)
            {
                return NotFound();
            }

            return View(datPhong);
        }

        // GET: DatPhong/Create - Modern Form
        public async Task<IActionResult> CreateModern()
        {
            _logger.LogInformation("Người dùng {0} mở form đặt phòng", User.Identity?.Name);

            var loaiPhongs = await _context.LoaiPhongs
                .OrderBy(lp => lp.TenLoaiPhong)
                .Select(lp => new { lp.MaLoaiPhong, lp.TenLoaiPhong })
                .ToListAsync();

            var selectListItems = loaiPhongs
                .Select(lp => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = lp.MaLoaiPhong,
                    Text = lp.TenLoaiPhong
                })
                .ToList();

            ViewBag.LoaiPhong = selectListItems;
            return View("CreateModern");
        }

        // GET: DatPhong/Create - Classic Form
        public async Task<IActionResult> Create()
        {
            _logger.LogInformation("Người dùng {0} mở form đặt phòng cơ bản", User.Identity?.Name);

            var loaiPhongs = await _context.LoaiPhongs
                .OrderBy(lp => lp.TenLoaiPhong)
                .Select(lp => new { lp.MaLoaiPhong, lp.TenLoaiPhong })
                .ToListAsync();

            var selectListItems = loaiPhongs
                .Select(lp => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = lp.MaLoaiPhong,
                    Text = lp.TenLoaiPhong
                })
                .ToList();

            ViewBag.LoaiPhong = selectListItems;
            return View();
        }

        // POST: DatPhong/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaDatPhong,MaKhachHang,NgayDat,NgayNhanDuKien,NgayTraDuKien,MaLoaiPhong,SoLuong,GhiChu")] DatPhong datPhong, string MaLoaiPhong, int SoLuong, string GhiChu)
        {
            try
            {
                // Validate customer
                var khachHang = await _context.KhachHangs.FindAsync(datPhong.MaKhachHang);
                if (khachHang == null)
                {
                    _logger.LogWarning("Khách hàng {0} không tồn tại", datPhong.MaKhachHang);
                    ViewBag.ErrorMessage = "Khách hàng không tồn tại";
                    return RedirectToAction(nameof(CreateModern));
                }

                // Validate room type
                var loaiPhong = await _context.LoaiPhongs.FindAsync(MaLoaiPhong);
                if (loaiPhong == null)
                {
                    _logger.LogWarning("Loại phòng {0} không tồn tại", MaLoaiPhong);
                    ViewBag.ErrorMessage = "Loại phòng không tồn tại";
                    return RedirectToAction(nameof(CreateModern));
                }

                // Generate booking ID
                datPhong.MaDatPhong = GenerateMaDatPhong();
                datPhong.NgayDat = DateTime.Now;
                datPhong.TrangThai = "Chờ xác nhận";

                _context.Add(datPhong);
                await _context.SaveChangesAsync();

                // Add booking details
                var giaDatPhong = await _context.GiaPhongs
                    .Where(gp => gp.MaLoaiPhong == MaLoaiPhong && gp.NgayBatDau <= DateTime.Now && gp.NgayKetThuc >= DateTime.Now)
                    .FirstOrDefaultAsync();

                double giaTinh = giaDatPhong?.Gia ?? 0.0;
                int soNgay = (int)(((datPhong.NgayTraDuKien ?? DateTime.Now) - (datPhong.NgayNhanDuKien ?? DateTime.Now)).TotalDays);

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

                _logger.LogInformation("Người dùng {0} tạo đơn đặt phòng: {1} cho khách {2}", 
                    User.Identity?.Name, datPhong.MaDatPhong, khachHang.TenKhachHang);

                return RedirectToAction(nameof(Details), new { id = datPhong.MaDatPhong });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo đơn đặt phòng");
                ViewBag.ErrorMessage = "Có lỗi xảy ra khi tạo đơn đặt phòng";
                return RedirectToAction(nameof(CreateModern));
            }
        }

        // POST: DatPhong/CreateFromPublic - API endpoint for public booking
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateFromPublic([FromBody] PublicBookingRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.MaKhachHang) || string.IsNullOrEmpty(request.MaLoaiPhong))
                {
                    return Json(new { success = false, message = "Thông tin không hợp lệ" });
                }

                // Create booking
                var datPhong = new DatPhong
                {
                    MaDatPhong = GenerateMaDatPhong(),
                    MaKhachHang = request.MaKhachHang,
                    NgayDat = DateTime.Now,
                    NgayNhanDuKien = DateTime.Parse(request.NgayNhanDuKien),
                    NgayTraDuKien = DateTime.Parse(request.NgayTraDuKien),
                    TrangThai = "Chờ xác nhận"
                };

                _context.Add(datPhong);

                // Add booking details
                var giaDatPhong = await _context.GiaPhongs
                    .Where(gp => gp.MaLoaiPhong == request.MaLoaiPhong && gp.NgayBatDau <= DateTime.Now && gp.NgayKetThuc >= DateTime.Now)
                    .FirstOrDefaultAsync();

                double giaTinh = giaDatPhong?.Gia ?? 0.0;
                int soNgay = (int)(((datPhong.NgayTraDuKien ?? DateTime.Now) - (datPhong.NgayNhanDuKien ?? DateTime.Now)).TotalDays);

                for (int i = 0; i < request.SoLuong; i++)
                {
                    var ctdatPhong = new CtdatPhong
                    {
                        MaDatPhong = datPhong.MaDatPhong,
                        MaLoaiPhong = request.MaLoaiPhong,
                        SoLuong = 1,
                        GiaTamTinh = giaTinh * soNgay
                    };
                    _context.Add(ctdatPhong);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Khách hàng công khai {0} tạo đơn đặt phòng: {1}", 
                    request.MaKhachHang, datPhong.MaDatPhong);

                return Json(new { success = true, maDatPhong = datPhong.MaDatPhong });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo đơn đặt phòng từ công khai");
                return Json(new { success = false, message = "Lỗi máy chủ" });
            }
        }

        // GET: DatPhong/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var datPhong = await _context.DatPhongs.FindAsync(id);
            if (datPhong == null)
            {
                return NotFound();
            }

            return View(datPhong);
        }

        // POST: DatPhong/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("MaDatPhong,MaKhachHang,NgayDat,NgayNhanDuKien,NgayTraDuKien,TrangThai")] DatPhong datPhong)
        {
            if (id != datPhong.MaDatPhong)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(datPhong);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Người dùng {0} cập nhật đơn đặt phòng: {1}", 
                        User.Identity?.Name, datPhong.MaDatPhong);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DatPhongExists(datPhong.MaDatPhong))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(datPhong);
        }

        // GET: DatPhong/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var datPhong = await _context.DatPhongs
                .Include(dp => dp.MaKhachHangNavigation)
                .Include(dp => dp.CtdatPhongs)
                .FirstOrDefaultAsync(m => m.MaDatPhong == id);

            if (datPhong == null)
            {
                return NotFound();
            }

            return View(datPhong);
        }

        // POST: DatPhong/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var datPhong = await _context.DatPhongs
                .Include(dp => dp.CtdatPhongs)
                .FirstOrDefaultAsync(m => m.MaDatPhong == id);

            if (datPhong != null)
            {
                // Remove booking details
                _context.CtdatPhongs.RemoveRange(datPhong.CtdatPhongs);
                
                // Remove booking
                _context.DatPhongs.Remove(datPhong);
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Người dùng {0} xóa đơn đặt phòng: {1}", 
                    User.Identity?.Name, datPhong.MaDatPhong);
            }

            return RedirectToAction(nameof(Index));
        }

        // API: Get Available Rooms
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableRooms([FromBody] RoomSearchRequest request)
        {
            try
            {
                // Parse dates if they are strings
                DateTime ngayNhan = request.NgayNhan;
                DateTime ngayTra = request.NgayTra;

                if (ngayNhan == default(DateTime) || ngayTra == default(DateTime))
                {
                    return Json(new { success = false, message = "Thông tin không hợp lệ" });
                }

                if (string.IsNullOrEmpty(request.MaLoaiPhong))
                {
                    return Json(new { success = false, message = "Loại phòng không hợp lệ" });
                }

                // Get room type info
                var loaiPhong = await _context.LoaiPhongs.FindAsync(request.MaLoaiPhong);
                if (loaiPhong == null)
                {
                    return Json(new { success = false, message = "Loại phòng không tồn tại" });
                }

                // Get booked rooms in the date range
                var bookedRoomIds = await _context.ThuePhongs
                    .Include(tp => tp.CtthuePhongs)
                    .Where(tp => 
                        tp.NgayNhan < ngayTra && 
                        tp.NgayTra > ngayNhan
                    )
                    .SelectMany(tp => tp.CtthuePhongs)
                    .Where(ct => ct.MaPhongNavigation.MaLoaiPhong == request.MaLoaiPhong)
                    .Select(ct => ct.MaPhong)
                    .Distinct()
                    .ToListAsync();

                // Get available rooms
                var availableRooms = await _context.Phongs
                    .Where(p => p.MaLoaiPhong == request.MaLoaiPhong && !bookedRoomIds.Contains(p.MaPhong))
                    .Select(p => new { p.MaPhong, p.MaLoaiPhong, TenLoaiPhong = p.MaLoaiPhongNavigation.TenLoaiPhong })
                    .ToListAsync();

                // Get pricing
                var giaPhong = await _context.GiaPhongs
                    .Where(gp => gp.MaLoaiPhong == request.MaLoaiPhong && 
                                 gp.NgayBatDau <= DateTime.Now && 
                                 gp.NgayKetThuc >= DateTime.Now)
                    .FirstOrDefaultAsync();

                double gia = giaPhong?.Gia ?? 0.0;
                int soNgay = (ngayTra.Date - ngayNhan.Date).Days;
                if (soNgay <= 0) soNgay = 1;

                return Json(new
                {
                    success = availableRooms.Count > 0,
                    phongTrongs = availableRooms,
                    gia = gia,
                    soNgay = soNgay,
                    giaTinh = gia * soNgay,
                    message = availableRooms.Count > 0 ? $"Có {availableRooms.Count} phòng trống" : "Không có phòng trống"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tìm kiếm phòng trống");
                return Json(new { success = false, message = "Lỗi máy chủ" });
            }
        }

        // Private methods
        private bool DatPhongExists(string id)
        {
            return _context.DatPhongs.Any(e => e.MaDatPhong == id);
        }

        private string GenerateMaDatPhong()
        {
            var lastId = _context.DatPhongs
                .OrderByDescending(dp => dp.MaDatPhong)
                .Select(dp => dp.MaDatPhong)
                .FirstOrDefault() ?? "DP000000";

            int number = int.Parse(lastId.Substring(2)) + 1;
            return "DP" + number.ToString("D6");
        }
    }

    // Helper classes
    public class RoomSearchRequest
    {
        public string MaLoaiPhong { get; set; }
        public DateTime NgayNhan { get; set; }
        public DateTime NgayTra { get; set; }
    }

    public class PublicBookingRequest
    {
        public string MaKhachHang { get; set; }
        public string MaLoaiPhong { get; set; }
        public string NgayNhanDuKien { get; set; }
        public string NgayTraDuKien { get; set; }
        public int SoLuong { get; set; }
        public string GhiChu { get; set; }
    }
}
