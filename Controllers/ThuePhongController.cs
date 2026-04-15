using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebKhachSan.Models;
using WebKhachSan.ViewModels;

namespace WebKhachSan.Controllers
{
    [Authorize]
    public class ThuePhongController : Controller
    {
        private readonly QuanLyKhachSanContext _context;
        private readonly ILogger<ThuePhongController> _logger;

        public ThuePhongController(QuanLyKhachSanContext context, ILogger<ThuePhongController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var thuePhongs = await _context.ThuePhongs
                .Include(tp => tp.MaKhachHangNavigation)
                .Include(tp => tp.CtthuePhongs)
                    .ThenInclude(ct => ct.MaPhongNavigation)
                        .ThenInclude(p => p.MaLoaiPhongNavigation)
                .OrderByDescending(tp => tp.NgayNhan)
                .ToListAsync();

            return View(thuePhongs);
        }

        [HttpGet]
        public async Task<IActionResult> AvailableRoomsModal(string? maPhong, string? soPhong, string? maLoaiPhong)
        {
            var model = new DanhSachPhongTrongViewModel
            {
                MaPhong = maPhong,
                SoPhong = soPhong,
                MaLoaiPhong = maLoaiPhong,
                LoaiPhongs = await _context.LoaiPhongs.OrderBy(lp => lp.TenLoaiPhong).ToListAsync(),
                Phongs = await GetAvailableRoomsAsync(maPhong, soPhong, maLoaiPhong)
            };

            return PartialView("_AvailableRoomsModalBody", model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateOffline(string maPhong)
        {
            var model = await BuildOfflineRentalViewModelAsync(maPhong);
            if (model == null)
            {
                return Content("<div class='alert alert-danger mb-0'>Khong tim thay phong trong hop le.</div>", "text/html");
            }

            return PartialView("_CreateOfflineRentalForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOffline(ThuePhongOfflineViewModel model)
        {
            await PopulateOfflineRentalDisplayDataAsync(model);
            await ValidateOfflineRentalAsync(model);

            if (!ModelState.IsValid)
            {
                Response.StatusCode = 400;
                return PartialView("_CreateOfflineRentalForm", model);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var phong = await _context.Phongs
                    .Include(p => p.MaLoaiPhongNavigation)
                    .FirstAsync(p => p.MaPhong == model.MaPhong);

                var giaHienTai = await GetCurrentPriceAsync(phong.MaLoaiPhong);
                if (giaHienTai == null)
                {
                    ModelState.AddModelError(string.Empty, "Phong nay chua co gia hien tai, khong the lap phieu thue.");
                    Response.StatusCode = 400;
                    return PartialView("_CreateOfflineRentalForm", model);
                }

                var khachHang = await ResolveCustomerAsync(model);
                var maThuePhong = await GenerateNextCodeAsync(_context.ThuePhongs.Select(tp => tp.MaThuePhong), "TP");

                var thuePhong = new ThuePhong
                {
                    MaThuePhong = maThuePhong,
                    MaKhachHang = khachHang.MaKhachHang,
                    TrangThai = "Dang thue",
                    NgayNhan = model.NgayNhan?.Date ?? DateTime.Today,
                    NgayTra = model.NgayTra?.Date
                };

                var chiTiet = new CtthuePhong
                {
                    MaThuePhong = maThuePhong,
                    MaPhong = phong.MaPhong,
                    GiaThueTaiThoiDiem = giaHienTai.Gia
                };

                phong.TrangThai = "Có khách";

                _context.ThuePhongs.Add(thuePhong);
                _context.CtthuePhongs.Add(chiTiet);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Nguoi dung {User} tao phieu thue {MaThuePhong} cho phong {MaPhong}",
                    User.Identity?.Name, maThuePhong, phong.MaPhong);

                TempData["Success"] = $"Da thue phong {phong.SoPhong} cho khach {khachHang.TenKhachHang}.";
                return Json(new
                {
                    success = true,
                    message = $"Da xac nhan thue phong {phong.SoPhong} thanh cong."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Loi khi tao phieu thue offline cho phong {MaPhong}", model.MaPhong);
                ModelState.AddModelError(string.Empty, "Khong the xac nhan thue phong. Vui long thu lai.");
                Response.StatusCode = 500;
                return PartialView("_CreateOfflineRentalForm", model);
            }
        }

        [HttpGet]
        public IActionResult CreateStep1()
        {
            return PartialView("_CreateStep1");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStep1(ThuePhongStep1ViewModel model)
        {
            if (!ModelState.IsValid)
            {
                Response.StatusCode = 400;
                return PartialView("_CreateStep1", model);
            }

            var phongs = await GetAvailableRoomsAsync(null, null, null);
            var step2 = new ThuePhongStep2ViewModel
            {
                TenKhachHang = model.TenKhachHang,
                DienThoai = model.DienThoai,
                DiaChi = model.DiaChi,
                Cccd = model.Cccd,
                NgayNhan = DateTime.Today,
                Phongs = phongs
            };

            return PartialView("_CreateStep2", step2);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStep2(ThuePhongStep2ViewModel model)
        {
            if (model.SelectedPhongs == null || !model.SelectedPhongs.Any())
            {
                ModelState.AddModelError(string.Empty, "Vui long chon it nhat mot phong.");
            }

            if (!ModelState.IsValid)
            {
                model.Phongs = await GetAvailableRoomsAsync(null, null, null);
                Response.StatusCode = 400;
                return PartialView("_CreateStep2", model);
            }

            var phongsChon = new List<PhongChonViewModel>();
            foreach (var maPhong in model.SelectedPhongs)
            {
                var phong = await _context.Phongs
                    .Include(p => p.MaLoaiPhongNavigation)
                    .FirstOrDefaultAsync(p => p.MaPhong == maPhong);

                if (phong != null)
                {
                    var gia = await GetCurrentPriceAsync(phong.MaLoaiPhong);
                    phongsChon.Add(new PhongChonViewModel
                    {
                        MaPhong = phong.MaPhong,
                        SoPhong = phong.SoPhong,
                        TenLoaiPhong = phong.MaLoaiPhongNavigation?.TenLoaiPhong,
                        GiaThue = gia?.Gia ?? 0
                    });
                }
            }

            var confirm = new ThuePhongConfirmViewModel
            {
                Cccd = model.Cccd,
                TenKhachHang = model.TenKhachHang,
                DienThoai = model.DienThoai,
                DiaChi = model.DiaChi,
                NgayNhan = model.NgayNhan ?? DateTime.Today,
                PhongsChon = phongsChon,
                TongTien = phongsChon.Sum(p => p.GiaThue ?? 0)
            };

            return PartialView("_CreateConfirm", confirm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFinal(ThuePhongConfirmViewModel model)
        {
            if (model.PhongsChon == null || !model.PhongsChon.Any())
            {
                return Json(new { success = false, message = "Khong co phong nao duoc chon." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var khachHang = await ResolveCustomerAsync(model.Cccd, model.TenKhachHang, model.DienThoai, model.DiaChi);

                foreach (var phongChon in model.PhongsChon)
                {
                    var maThuePhong = await GenerateNextCodeAsync(_context.ThuePhongs.Select(tp => tp.MaThuePhong), "TP");
                    var gia = await GetCurrentPriceFromMaLoaiPhongAsync(phongChon.MaPhong);

                    var thuePhong = new ThuePhong
                    {
                        MaThuePhong = maThuePhong,
                        MaKhachHang = khachHang.MaKhachHang,
                        TrangThai = "Dang thue",
                        NgayNhan = model.NgayNhan,
                        NgayTra = null
                    };

                    var chiTiet = new CtthuePhong
                    {
                        MaThuePhong = maThuePhong,
                        MaPhong = phongChon.MaPhong,
                        GiaThueTaiThoiDiem = gia
                    };

                    var phong = await _context.Phongs.FindAsync(phongChon.MaPhong);
                    if (phong != null)
                    {
                        phong.TrangThai = "Có khách";
                    }

                    _context.ThuePhongs.Add(thuePhong);
                    _context.CtthuePhongs.Add(chiTiet);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Nguoi dung {User} tao phieu thue cho {Count} phong, khach hang {TenKhachHang}",
                    User.Identity?.Name, model.PhongsChon.Count, model.TenKhachHang);

                return Json(new { success = true, message = $"Da tao phieu thue cho {model.PhongsChon.Count} phong thanh cong." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Loi khi tao phieu thue");
                return Json(new { success = false, message = "Khong the tao phieu thue. Vui long thu lai." });
            }
        }

        private async Task<KhachHang> ResolveCustomerAsync(string? cccd, string tenKhachHang, string? dienThoai, string? diaChi)
        {
            KhachHang? khachHang = null;

            if (!string.IsNullOrWhiteSpace(cccd))
            {
                khachHang = await _context.KhachHangs.FirstOrDefaultAsync(k => k.Cccd == cccd);
            }

            if (khachHang == null)
            {
                var maKhachHang = await GenerateNextCodeAsync(_context.KhachHangs.Select(k => k.MaKhachHang), "KH");
                khachHang = new KhachHang { MaKhachHang = maKhachHang };
                _context.KhachHangs.Add(khachHang);
            }

            khachHang.TenKhachHang = tenKhachHang.Trim();
            khachHang.DienThoai = dienThoai?.Trim();
            khachHang.DiaChi = diaChi?.Trim();
            khachHang.Cccd = string.IsNullOrWhiteSpace(cccd) ? null : cccd.Trim();

            await _context.SaveChangesAsync();
            return khachHang;
        }

        private async Task<double?> GetCurrentPriceFromMaLoaiPhongAsync(string maPhong)
        {
            var phong = await _context.Phongs.FindAsync(maPhong);
            if (phong == null) return null;
            var gia = await GetCurrentPriceAsync(phong.MaLoaiPhong);
            return gia?.Gia;
        }

        [HttpGet]
        public async Task<IActionResult> TraPhong(string id)
        {
            var thuePhong = await _context.ThuePhongs
                .Include(tp => tp.MaKhachHangNavigation)
                .Include(tp => tp.CtthuePhongs)
                    .ThenInclude(ct => ct.MaPhongNavigation)
                .FirstOrDefaultAsync(tp => tp.MaThuePhong == id);

            if (thuePhong == null)
            {
                return NotFound();
            }

            var model = new TraPhongViewModel
            {
                MaThuePhong = thuePhong.MaThuePhong,
                TenKhachHang = thuePhong.MaKhachHangNavigation?.TenKhachHang,
                SoPhong = thuePhong.CtthuePhongs.FirstOrDefault()?.MaPhongNavigation?.SoPhong,
                NgayNhan = thuePhong.NgayNhan,
                NgayTra = DateTime.Today
            };

            return PartialView("_TraPhongModal", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TraPhong(TraPhongViewModel model)
        {
            if (!ModelState.IsValid)
            {
                Response.StatusCode = 400;
                return PartialView("_TraPhongModal", model);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var thuePhong = await _context.ThuePhongs
                    .Include(tp => tp.CtthuePhongs)
                    .FirstOrDefaultAsync(tp => tp.MaThuePhong == model.MaThuePhong);

                if (thuePhong == null)
                {
                    return NotFound();
                }

                thuePhong.NgayTra = model.NgayTra;
                thuePhong.TrangThai = "Da tra";

                foreach (var ct in thuePhong.CtthuePhongs)
                {
                    var phong = await _context.Phongs.FindAsync(ct.MaPhong);
                    if (phong != null)
                    {
                        phong.TrangThai = "Trống";
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Nguoi dung {User} tra phong cho phieu thue {MaThuePhong}, NgayTra: {NgayTra}",
                    User.Identity?.Name, model.MaThuePhong, model.NgayTra);

                TempData["Success"] = $"Da tra phong thanh cong.";
                return Json(new { success = true, message = "Da tra phong thanh cong." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Loi khi tra phong {MaThuePhong}", model.MaThuePhong);
                ModelState.AddModelError(string.Empty, "Khong the tra phong. Vui long thu lai.");
                Response.StatusCode = 500;
                return PartialView("_TraPhongModal", model);
            }
        }

        private async Task<List<PhongTrongItemViewModel>> GetAvailableRoomsAsync(string? maPhong, string? soPhong, string? maLoaiPhong)
        {
            var query = _context.Phongs
                .Include(p => p.MaLoaiPhongNavigation)
                .Where(p => p.TrangThai == "Trống");

            if (!string.IsNullOrWhiteSpace(maPhong))
            {
                query = query.Where(p => p.MaPhong.Contains(maPhong));
            }

            if (!string.IsNullOrWhiteSpace(soPhong))
            {
                query = query.Where(p => p.SoPhong != null && p.SoPhong.Contains(soPhong));
            }

            if (!string.IsNullOrWhiteSpace(maLoaiPhong))
            {
                query = query.Where(p => p.MaLoaiPhong == maLoaiPhong);
            }

            var phongs = await query.OrderBy(p => p.SoPhong).ToListAsync();
            var roomTypeIds = phongs.Select(p => p.MaLoaiPhong).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var today = DateTime.Today;

            var priceMap = await _context.GiaPhongs
                .Where(g => g.MaLoaiPhong != null
                    && roomTypeIds.Contains(g.MaLoaiPhong)
                    && g.NgayBatDau <= today
                    && (g.NgayKetThuc == null || g.NgayKetThuc >= today))
                .OrderByDescending(g => g.NgayBatDau)
                .ToListAsync();

            return phongs.Select(p => new PhongTrongItemViewModel
            {
                MaPhong = p.MaPhong,
                SoPhong = p.SoPhong,
                MaLoaiPhong = p.MaLoaiPhong,
                TenLoaiPhong = p.MaLoaiPhongNavigation?.TenLoaiPhong,
                SoNguoiToiDa = p.MaLoaiPhongNavigation?.SoNguoiToiDa,
                GiaHienTai = priceMap.FirstOrDefault(g => g.MaLoaiPhong == p.MaLoaiPhong)?.Gia
            }).ToList();
        }

        private async Task<ThuePhongOfflineViewModel?> BuildOfflineRentalViewModelAsync(string maPhong)
        {
            var phong = await _context.Phongs
                .Include(p => p.MaLoaiPhongNavigation)
                .FirstOrDefaultAsync(p => p.MaPhong == maPhong && p.TrangThai == "Trống");

            if (phong == null)
            {
                return null;
            }

            var giaHienTai = await GetCurrentPriceAsync(phong.MaLoaiPhong);

            return new ThuePhongOfflineViewModel
            {
                MaPhong = phong.MaPhong,
                SoPhong = phong.SoPhong,
                TenLoaiPhong = phong.MaLoaiPhongNavigation?.TenLoaiPhong,
                GiaHienTai = giaHienTai?.Gia,
                NgayNhan = DateTime.Today,
                NgayTra = null
            };
        }

        private async Task PopulateOfflineRentalDisplayDataAsync(ThuePhongOfflineViewModel model)
        {
            var phong = await _context.Phongs
                .Include(p => p.MaLoaiPhongNavigation)
                .FirstOrDefaultAsync(p => p.MaPhong == model.MaPhong);

            if (phong == null)
            {
                return;
            }

            model.SoPhong = phong.SoPhong;
            model.TenLoaiPhong = phong.MaLoaiPhongNavigation?.TenLoaiPhong;
            model.GiaHienTai = (await GetCurrentPriceAsync(phong.MaLoaiPhong))?.Gia;
        }

        private async Task ValidateOfflineRentalAsync(ThuePhongOfflineViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.MaPhong))
            {
                ModelState.AddModelError(nameof(model.MaPhong), "Phong khong hop le.");
                return;
            }

            if (!model.NgayNhan.HasValue)
            {
                ModelState.AddModelError(nameof(model.NgayNhan), "Vui long chon ngay nhan phong.");
            }

            if (model.NgayTra.HasValue && model.NgayNhan.HasValue && model.NgayTra.Value.Date < model.NgayNhan.Value.Date)
            {
                ModelState.AddModelError(nameof(model.NgayTra), "Ngay tra phong phai lon hon hoac bang ngay nhan phong.");
            }

            var phong = await _context.Phongs.FirstOrDefaultAsync(p => p.MaPhong == model.MaPhong);
            if (phong == null)
            {
                ModelState.AddModelError(string.Empty, "Phong khong ton tai.");
                return;
            }

            if (!string.Equals(NormalizeTrangThai(phong.TrangThai), "Trống", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Phong nay khong con trong de cho thue.");
            }

            var hasActiveRental = await _context.CtthuePhongs
                .Include(ct => ct.MaThuePhongNavigation)
                .AnyAsync(ct => ct.MaPhong == model.MaPhong && ct.MaThuePhongNavigation.NgayTra == null);

            if (hasActiveRental)
            {
                ModelState.AddModelError(string.Empty, "Phong nay dang co phieu thue chua ket thuc.");
            }

            if (await GetCurrentPriceAsync(phong.MaLoaiPhong) == null)
            {
                ModelState.AddModelError(string.Empty, "Loai phong nay chua co gia hien tai.");
            }
        }

        private async Task<GiaPhong?> GetCurrentPriceAsync(string? maLoaiPhong)
        {
            if (string.IsNullOrWhiteSpace(maLoaiPhong))
            {
                return null;
            }

            var today = DateTime.Today;
            return await _context.GiaPhongs
                .Where(g => g.MaLoaiPhong == maLoaiPhong
                    && g.NgayBatDau <= today
                    && (g.NgayKetThuc == null || g.NgayKetThuc >= today))
                .OrderByDescending(g => g.NgayBatDau)
                .FirstOrDefaultAsync();
        }

        private async Task<KhachHang> ResolveCustomerAsync(ThuePhongOfflineViewModel model)
        {
            KhachHang? khachHang = null;

            if (!string.IsNullOrWhiteSpace(model.Cccd))
            {
                khachHang = await _context.KhachHangs.FirstOrDefaultAsync(k => k.Cccd == model.Cccd);
            }

            if (khachHang == null)
            {
                var maKhachHang = await GenerateNextCodeAsync(_context.KhachHangs.Select(k => k.MaKhachHang), "KH");
                khachHang = new KhachHang
                {
                    MaKhachHang = maKhachHang
                };
                _context.KhachHangs.Add(khachHang);
            }

            khachHang.TenKhachHang = model.TenKhachHang?.Trim();
            khachHang.DienThoai = model.DienThoai?.Trim();
            khachHang.DiaChi = model.DiaChi?.Trim();
            khachHang.Cccd = string.IsNullOrWhiteSpace(model.Cccd) ? null : model.Cccd.Trim();

            await _context.SaveChangesAsync();
            return khachHang;
        }

        private async Task<string> GenerateNextCodeAsync(IQueryable<string> source, string prefix)
        {
            var ids = await source.Where(id => id != null && id.StartsWith(prefix)).ToListAsync();
            var max = ids.Select(id =>
            {
                var digits = new string(id.Skip(prefix.Length).Where(char.IsDigit).ToArray());
                return int.TryParse(digits, out var number) ? number : 0;
            }).DefaultIfEmpty(0).Max();

            return $"{prefix}{max + 1:D3}";
        }

        private static string NormalizeTrangThai(string? trangThai)
        {
            var normalized = (trangThai ?? string.Empty).Trim();

            return normalized switch
            {
                "Tr?ng" or "Trá»‘ng" or "Trống" => "Trống",
                "CÃ³ khÃ¡ch" or "Có khách" => "Có khách",
                "B?o trì" or "Báº£o trÃ¬" or "Bảo trì" => "Bảo trì",
                "Ðã d?t" or "ÄÃ£ Ä‘áº·t" or "Đã đặt" => "Đã đặt",
                _ => normalized
            };
        }
    }
}
