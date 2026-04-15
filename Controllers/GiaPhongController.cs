using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebKhachSan.Models;
using WebKhachSan.ViewModels;

namespace WebKhachSan.Controllers
{
    [Authorize]
    public class GiaPhongController : Controller
    {
        private readonly QuanLyKhachSanContext _context;
        private readonly ILogger<GiaPhongController> _logger;

        public GiaPhongController(QuanLyKhachSanContext context, ILogger<GiaPhongController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? maLoaiPhong)
        {
            _logger.LogInformation("Nguoi dung {User} xem danh sach gia phong", User.Identity?.Name);

            IQueryable<GiaPhong> giaPhongs = _context.GiaPhongs
                .Include(g => g.MaLoaiPhongNavigation)
                .OrderByDescending(g => g.NgayBatDau);

            if (!string.IsNullOrEmpty(maLoaiPhong))
            {
                giaPhongs = giaPhongs.Where(g => g.MaLoaiPhong == maLoaiPhong);
                ViewBag.MaLoaiPhong = maLoaiPhong;
                ViewBag.TenLoaiPhong = await _context.LoaiPhongs
                    .Where(l => l.MaLoaiPhong == maLoaiPhong)
                    .Select(l => l.TenLoaiPhong)
                    .FirstOrDefaultAsync();
            }

            return View(await giaPhongs.ToListAsync());
        }

        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var giaPhong = await _context.GiaPhongs
                .Include(g => g.MaLoaiPhongNavigation)
                .FirstOrDefaultAsync(g => g.MaGia == id);

            if (giaPhong == null)
            {
                return NotFound();
            }

            var phongsApDung = await _context.Phongs
                .Include(p => p.MaLoaiPhongNavigation)
                .Where(p => p.MaLoaiPhong == giaPhong.MaLoaiPhong)
                .OrderBy(p => p.SoPhong)
                .ToListAsync();

            return View(new GiaPhongDetailsViewModel
            {
                GiaPhong = giaPhong,
                PhongsApDung = phongsApDung
            });
        }

        [HttpGet]
        public async Task<IActionResult> BulkSetPrice(DateTime? tuNgay, DateTime? denNgay)
        {
            var model = await BuildBulkSetPriceViewModelAsync(tuNgay, denNgay);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkSetPrice(BulkPriceInputViewModel model)
        {
            if (model.SelectedPhongs == null || !model.SelectedPhongs.Any())
            {
                ModelState.AddModelError(string.Empty, "Vui long chon it nhat mot phong.");
            }

            if (model.Gia <= 0)
            {
                ModelState.AddModelError(nameof(model.Gia), "Gia phong phai lon hon 0.");
            }

            if (!ModelState.IsValid)
            {
                var vm = await BuildBulkSetPriceViewModelAsync(model.NgayBatDau, model.NgayKetThuc);
                return View(vm);
            }

            try
            {
                var nextNumber = await GetNextMaGiaNumberAsync();
                var giaPhongs = new List<GiaPhong>();
                var count = 0;

                foreach (var maPhong in model.SelectedPhongs)
                {
                    giaPhongs.Add(new GiaPhong
                    {
                        MaGia = $"G{nextNumber + count:D3}",
                        MaLoaiPhong = model.Phongs.FirstOrDefault(p => p.MaPhong == maPhong)?.MaLoaiPhong,
                        Gia = model.Gia,
                        NgayBatDau = model.NgayBatDau,
                        NgayKetThuc = model.NgayKetThuc
                    });
                    count++;
                }

                _context.GiaPhongs.AddRange(giaPhongs);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Nguoi dung {User} dat gia hang loat cho {Count} phong, gia {Gia}",
                    User.Identity?.Name, giaPhongs.Count, model.Gia);

                TempData["Success"] = $"Da dat gia {model.Gia:N0} VND cho {giaPhongs.Count} phong thanh cong.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loi khi dat gia phong hang loat");
                ModelState.AddModelError(string.Empty, "Khong the dat gia phong. Vui long thu lai.");
                var vm = await BuildBulkSetPriceViewModelAsync(model.NgayBatDau, model.NgayKetThuc);
                return View(vm);
            }
        }

        private async Task<BulkSetPriceViewModel> BuildBulkSetPriceViewModelAsync(DateTime? tuNgay, DateTime? denNgay)
        {
            var startDate = tuNgay ?? DateTime.Today;
            var endDate = denNgay ?? DateTime.Today.AddMonths(1);

            var phongs = await _context.Phongs
                .Include(p => p.MaLoaiPhongNavigation)
                .Where(p => p.TrangThai == "Trống" || p.TrangThai == "Có khách")
                .OrderBy(p => p.MaLoaiPhong)
                .ThenBy(p => p.SoPhong)
                .ToListAsync();

            var phongsCoGia = await _context.GiaPhongs
                .Where(g => g.MaLoaiPhong != null
                    && g.NgayBatDau <= endDate
                    && (g.NgayKetThuc == null || g.NgayKetThuc >= startDate))
                .Select(g => g.MaLoaiPhong)
                .Distinct()
                .ToListAsync();

            var phongsChuaCoGia = phongs
                .Where(p => !phongsCoGia.Contains(p.MaLoaiPhong))
                .Select(p => new PhongChuaCoGiaViewModel
                {
                    MaPhong = p.MaPhong,
                    SoPhong = p.SoPhong,
                    MaLoaiPhong = p.MaLoaiPhong ?? string.Empty,
                    TenLoaiPhong = p.MaLoaiPhongNavigation?.TenLoaiPhong,
                    Selected = false
                })
                .ToList();

            return new BulkSetPriceViewModel
            {
                NgayBatDau = startDate,
                NgayKetThuc = endDate,
                PhongsChuaCoGia = phongsChuaCoGia
            };
        }

        public async Task<IActionResult> Create(string? maLoaiPhong)
        {
            await PopulateLoaiPhongAsync(maLoaiPhong);
            return View(new GiaPhong { MaLoaiPhong = maLoaiPhong });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaGia,MaLoaiPhong,Gia,NgayBatDau,NgayKetThuc")] GiaPhong giaPhong)
        {
            await ValidateGiaPhongAsync(giaPhong);

            if (!ModelState.IsValid)
            {
                await PopulateLoaiPhongAsync(giaPhong.MaLoaiPhong);
                return View(giaPhong);
            }

            _context.GiaPhongs.Add(giaPhong);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Nguoi dung {User} tao gia phong {MaGia} cho loai phong {MaLoaiPhong}",
                User.Identity?.Name, giaPhong.MaGia, giaPhong.MaLoaiPhong);

            TempData["Success"] = "Them gia phong thanh cong.";
            return RedirectToAction(nameof(Index), new { maLoaiPhong = giaPhong.MaLoaiPhong });
        }

        [HttpGet]
        public async Task<IActionResult> CreateBulk([FromQuery] string[] selectedLoaiPhongIds)
        {
            if (selectedLoaiPhongIds == null || selectedLoaiPhongIds.Length == 0)
            {
                TempData["Error"] = "Hay chon it nhat mot loai phong de gan gia.";
                return RedirectToAction("Index", "LoaiPhong");
            }

            var model = await BuildBulkCreateViewModelAsync(selectedLoaiPhongIds);
            if (!model.LoaiPhongApDung.Any())
            {
                TempData["Error"] = "Khong tim thay loai phong da chon.";
                return RedirectToAction("Index", "LoaiPhong");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBulk(GiaPhongBulkCreateViewModel model)
        {
            model.LoaiPhongApDung = await GetSelectedLoaiPhongItemsAsync(model.SelectedLoaiPhongIds);

            if (!model.LoaiPhongApDung.Any())
            {
                ModelState.AddModelError(string.Empty, "Khong tim thay loai phong da chon.");
            }

            ValidateGiaPhongInput(model.Gia, model.NgayBatDau, model.NgayKetThuc);

            foreach (var loaiPhong in model.LoaiPhongApDung)
            {
                if (await HasOverlappingPriceRangeAsync(loaiPhong.MaLoaiPhong, model.NgayBatDau, model.NgayKetThuc))
                {
                    ModelState.AddModelError(string.Empty,
                        $"Loai phong '{loaiPhong.TenLoaiPhong ?? loaiPhong.MaLoaiPhong}' da co bang gia trung thoi gian ap dung.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var nextPriceNumber = await GetNextMaGiaNumberAsync();
            var giaPhongsMoi = model.LoaiPhongApDung.Select(loaiPhong => new GiaPhong
            {
                MaGia = $"G{nextPriceNumber++:D3}",
                MaLoaiPhong = loaiPhong.MaLoaiPhong,
                Gia = model.Gia,
                NgayBatDau = model.NgayBatDau,
                NgayKetThuc = model.NgayKetThuc
            }).ToList();

            _context.GiaPhongs.AddRange(giaPhongsMoi);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Nguoi dung {User} gan gia hang loat cho {TypeCount} loai phong",
                User.Identity?.Name, model.LoaiPhongApDung.Count);

            TempData["Success"] = $"Da tao {giaPhongsMoi.Count} bang gia cho cac loai phong duoc chon.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var giaPhong = await _context.GiaPhongs
                .Include(g => g.MaLoaiPhongNavigation)
                .FirstOrDefaultAsync(g => g.MaGia == id);

            if (giaPhong == null)
            {
                return NotFound();
            }

            await PopulateLoaiPhongAsync(giaPhong.MaLoaiPhong);
            return View(giaPhong);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("MaGia,MaLoaiPhong,Gia,NgayBatDau,NgayKetThuc")] GiaPhong giaPhong)
        {
            if (id != giaPhong.MaGia)
            {
                return NotFound();
            }

            await ValidateGiaPhongAsync(giaPhong, giaPhong.MaGia);

            if (!ModelState.IsValid)
            {
                await PopulateLoaiPhongAsync(giaPhong.MaLoaiPhong);
                return View(giaPhong);
            }

            try
            {
                _context.Update(giaPhong);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cap nhat gia phong thanh cong.";
                return RedirectToAction(nameof(Index), new { maLoaiPhong = giaPhong.MaLoaiPhong });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await GiaPhongExists(giaPhong.MaGia))
                {
                    return NotFound();
                }

                throw;
            }
        }

        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var giaPhong = await _context.GiaPhongs
                .Include(g => g.MaLoaiPhongNavigation)
                .FirstOrDefaultAsync(g => g.MaGia == id);

            if (giaPhong == null)
            {
                return NotFound();
            }

            return View(giaPhong);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var giaPhong = await _context.GiaPhongs.FindAsync(id);
            if (giaPhong == null)
            {
                return NotFound();
            }

            var maLoaiPhong = giaPhong.MaLoaiPhong;
            _context.GiaPhongs.Remove(giaPhong);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xoa gia phong thanh cong.";
            return RedirectToAction(nameof(Index), new { maLoaiPhong });
        }

        private async Task PopulateLoaiPhongAsync(string? maLoaiPhong = null)
        {
            ViewBag.LoaiPhongs = await _context.LoaiPhongs.OrderBy(l => l.TenLoaiPhong).ToListAsync();
            ViewBag.MaLoaiPhong = maLoaiPhong;
        }

        private void ValidateGiaPhongInput(double? gia, DateTime? ngayBatDau, DateTime? ngayKetThuc)
        {
            if (!gia.HasValue || gia.Value <= 0)
            {
                ModelState.AddModelError("Gia", "Gia phong phai lon hon 0.");
            }

            if (!ngayBatDau.HasValue)
            {
                ModelState.AddModelError("NgayBatDau", "Ngay bat dau khong duoc de trong.");
            }

            if (ngayBatDau.HasValue && ngayKetThuc.HasValue && ngayBatDau.Value.Date > ngayKetThuc.Value.Date)
            {
                ModelState.AddModelError("NgayKetThuc", "Ngay ket thuc phai sau hoac bang ngay bat dau.");
            }
        }

        private async Task ValidateGiaPhongAsync(GiaPhong giaPhong, string? excludeMaGia = null)
        {
            ValidateGiaPhongInput(giaPhong.Gia, giaPhong.NgayBatDau, giaPhong.NgayKetThuc);

            if (string.IsNullOrWhiteSpace(giaPhong.MaGia))
            {
                ModelState.AddModelError("MaGia", "Ma gia khong duoc de trong.");
            }
            else if (excludeMaGia == null && await _context.GiaPhongs.AnyAsync(g => g.MaGia == giaPhong.MaGia))
            {
                ModelState.AddModelError("MaGia", "Ma gia da ton tai.");
            }

            if (string.IsNullOrWhiteSpace(giaPhong.MaLoaiPhong))
            {
                ModelState.AddModelError("MaLoaiPhong", "Loai phong khong duoc de trong.");
            }
            else if (!await _context.LoaiPhongs.AnyAsync(l => l.MaLoaiPhong == giaPhong.MaLoaiPhong))
            {
                ModelState.AddModelError("MaLoaiPhong", "Loai phong khong ton tai.");
            }

            if (!HasModelError("MaLoaiPhong")
                && !HasModelError("NgayBatDau")
                && !HasModelError("NgayKetThuc")
                && await HasOverlappingPriceRangeAsync(giaPhong.MaLoaiPhong!, giaPhong.NgayBatDau, giaPhong.NgayKetThuc, excludeMaGia))
            {
                ModelState.AddModelError(string.Empty, "Khoang thoi gian nay bi trung voi bang gia khac cua loai phong.");
            }
        }

        private async Task<bool> HasOverlappingPriceRangeAsync(string maLoaiPhong, DateTime? ngayBatDau, DateTime? ngayKetThuc, string? excludeMaGia = null)
        {
            if (!ngayBatDau.HasValue)
            {
                return false;
            }

            var start = ngayBatDau.Value.Date;
            var end = (ngayKetThuc ?? DateTime.MaxValue).Date;

            return await _context.GiaPhongs
                .Where(g => g.MaLoaiPhong == maLoaiPhong && g.MaGia != excludeMaGia)
                .AnyAsync(g =>
                    g.NgayBatDau.HasValue &&
                    start <= (g.NgayKetThuc ?? DateTime.MaxValue).Date &&
                    g.NgayBatDau.Value.Date <= end);
        }

        private async Task<List<LoaiPhongBulkSelectionItemViewModel>> GetSelectedLoaiPhongItemsAsync(IEnumerable<string> selectedLoaiPhongIds)
        {
            var normalizedIds = selectedLoaiPhongIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct()
                .ToList();

            if (!normalizedIds.Any())
            {
                return new List<LoaiPhongBulkSelectionItemViewModel>();
            }

            var loaiPhongs = await _context.LoaiPhongs
                .Include(l => l.Phongs)
                .Include(l => l.GiaPhongs)
                .Where(l => normalizedIds.Contains(l.MaLoaiPhong))
                .OrderBy(l => l.TenLoaiPhong)
                .ToListAsync();

            return loaiPhongs.Select(l => new LoaiPhongBulkSelectionItemViewModel
            {
                MaLoaiPhong = l.MaLoaiPhong,
                TenLoaiPhong = l.TenLoaiPhong,
                SoLuongPhong = l.Phongs.Count,
                GiaHienTai = l.GiaPhongs
                    .Where(g => g.NgayBatDau <= DateTime.Now && (g.NgayKetThuc == null || g.NgayKetThuc >= DateTime.Now))
                    .OrderByDescending(g => g.NgayBatDau)
                    .Select(g => g.Gia)
                    .FirstOrDefault()
            }).ToList();
        }

        private async Task<GiaPhongBulkCreateViewModel> BuildBulkCreateViewModelAsync(IEnumerable<string> selectedLoaiPhongIds)
        {
            var loaiPhongApDung = await GetSelectedLoaiPhongItemsAsync(selectedLoaiPhongIds);

            return new GiaPhongBulkCreateViewModel
            {
                SelectedLoaiPhongIds = loaiPhongApDung.Select(l => l.MaLoaiPhong).ToList(),
                LoaiPhongApDung = loaiPhongApDung,
                NgayBatDau = DateTime.Today
            };
        }

        private bool HasModelError(string key)
        {
            return ViewData.ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0;
        }

        private async Task<int> GetNextMaGiaNumberAsync()
        {
            var existingIds = await _context.GiaPhongs.Select(g => g.MaGia).ToListAsync();
            var maxNumber = existingIds
                .Select(id =>
                {
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        return 0;
                    }

                    var digits = new string(id.Where(char.IsDigit).ToArray());
                    return int.TryParse(digits, out var number) ? number : 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            return maxNumber + 1;
        }

        private async Task<bool> GiaPhongExists(string id)
        {
            return await _context.GiaPhongs.AnyAsync(e => e.MaGia == id);
        }
    }
}
