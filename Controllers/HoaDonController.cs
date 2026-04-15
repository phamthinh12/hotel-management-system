using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebKhachSan.Models;

namespace WebKhachSan.Controllers
{
    [Authorize]
    public class HoaDonController : Controller
    {
        private readonly QuanLyKhachSanContext _context;
        private readonly ILogger<HoaDonController> _logger;

        public HoaDonController(QuanLyKhachSanContext context, ILogger<HoaDonController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: HoaDon
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Người dùng {0} xem danh sách hóa đơn", User.Identity?.Name);
            
            var hoaDons = await _context.HoaDons
                .Include(hd => hd.MaThuePhongNavigation)
                    .ThenInclude(tp => tp.MaKhachHangNavigation)
                .Include(hd => hd.MaNhanVienNavigation)
                .Include(hd => hd.CthoaDons)
                .OrderByDescending(hd => hd.NgayLap)
                .ToListAsync();

            return View(hoaDons);
        }

        // GET: HoaDon/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var hoaDon = await _context.HoaDons
                .Include(hd => hd.MaThuePhongNavigation)
                    .ThenInclude(tp => tp.MaKhachHangNavigation)
                .Include(hd => hd.MaNhanVienNavigation)
                .Include(hd => hd.CthoaDons)
                .FirstOrDefaultAsync(m => m.MaHoaDon == id);

            if (hoaDon == null)
            {
                return NotFound();
            }

            _logger.LogInformation("Người dùng {0} xem chi tiết hóa đơn: {1}", User.Identity?.Name, id);

            return View(hoaDon);
        }

        // GET: HoaDon/Create
        public async Task<IActionResult> Create()
        {
            var thuePhongs = await _context.ThuePhongs
                .Include(tp => tp.MaKhachHangNavigation)
                .OrderBy(tp => tp.MaThuePhong)
                .ToListAsync();

            var nhanViens = await _context.NhanViens
                .OrderBy(nv => nv.TenNhanVien)
                .ToListAsync();

            ViewBag.ThuePhongs = thuePhongs;
            ViewBag.NhanViens = nhanViens;

            return View();
        }

        // POST: HoaDon/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaHoaDon,MaThuePhong,MaNhanVien,NgayLap,TongTien")] HoaDon hoaDon)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra mã hóa đơn đã tồn tại
                if (await _context.HoaDons.AnyAsync(hd => hd.MaHoaDon == hoaDon.MaHoaDon))
                {
                    ModelState.AddModelError("MaHoaDon", "Mã hóa đơn đã tồn tại");
                }
                else
                {
                    // Kiểm tra mã thuê phòng có tồn tại
                    if (!await _context.ThuePhongs.AnyAsync(tp => tp.MaThuePhong == hoaDon.MaThuePhong))
                    {
                        ModelState.AddModelError("MaThuePhong", "Mã thuê phòng không tồn tại");
                    }
                    // Kiểm tra mã nhân viên có tồn tại
                    else if (!string.IsNullOrEmpty(hoaDon.MaNhanVien) && !await _context.NhanViens.AnyAsync(nv => nv.MaNhanVien == hoaDon.MaNhanVien))
                    {
                        ModelState.AddModelError("MaNhanVien", "Mã nhân viên không tồn tại");
                    }
                    else
                    {
                        if (hoaDon.NgayLap == null)
                            hoaDon.NgayLap = DateTime.Now;

                        _context.Add(hoaDon);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Người dùng {0} tạo hóa đơn mới: {1}", User.Identity?.Name, hoaDon.MaHoaDon);

                        TempData["Success"] = "Tạo hóa đơn thành công";
                        return RedirectToAction(nameof(Details), new { id = hoaDon.MaHoaDon });
                    }
                }
            }

            var thuePhongs = await _context.ThuePhongs
                .Include(tp => tp.MaKhachHangNavigation)
                .OrderBy(tp => tp.MaThuePhong)
                .ToListAsync();

            var nhanViens = await _context.NhanViens
                .OrderBy(nv => nv.TenNhanVien)
                .ToListAsync();

            ViewBag.ThuePhongs = thuePhongs;
            ViewBag.NhanViens = nhanViens;

            return View(hoaDon);
        }

        // GET: HoaDon/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var hoaDon = await _context.HoaDons.FindAsync(id);
            if (hoaDon == null)
            {
                return NotFound();
            }

            var thuePhongs = await _context.ThuePhongs
                .Include(tp => tp.MaKhachHangNavigation)
                .OrderBy(tp => tp.MaThuePhong)
                .ToListAsync();

            var nhanViens = await _context.NhanViens
                .OrderBy(nv => nv.TenNhanVien)
                .ToListAsync();

            ViewBag.ThuePhongs = thuePhongs;
            ViewBag.NhanViens = nhanViens;

            return View(hoaDon);
        }

        // POST: HoaDon/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("MaHoaDon,MaThuePhong,MaNhanVien,NgayLap,TongTien")] HoaDon hoaDon)
        {
            if (id != hoaDon.MaHoaDon)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(hoaDon);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Người dùng {0} cập nhật hóa đơn: {1}", User.Identity?.Name, hoaDon.MaHoaDon);
                    
                    TempData["Success"] = "Cập nhật hóa đơn thành công";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!HoaDonExists(hoaDon.MaHoaDon))
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

            var thuePhongs = await _context.ThuePhongs
                .Include(tp => tp.MaKhachHangNavigation)
                .OrderBy(tp => tp.MaThuePhong)
                .ToListAsync();

            var nhanViens = await _context.NhanViens
                .OrderBy(nv => nv.TenNhanVien)
                .ToListAsync();

            ViewBag.ThuePhongs = thuePhongs;
            ViewBag.NhanViens = nhanViens;

            return View(hoaDon);
        }

        // GET: HoaDon/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var hoaDon = await _context.HoaDons
                .Include(hd => hd.MaThuePhongNavigation)
                .Include(hd => hd.MaNhanVienNavigation)
                .Include(hd => hd.CthoaDons)
                .FirstOrDefaultAsync(m => m.MaHoaDon == id);

            if (hoaDon == null)
            {
                return NotFound();
            }

            return View(hoaDon);
        }

        // POST: HoaDon/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var hoaDon = await _context.HoaDons
                .Include(hd => hd.CthoaDons)
                .FirstOrDefaultAsync(hd => hd.MaHoaDon == id);

            if (hoaDon == null)
            {
                return NotFound();
            }

            // Xóa chi tiết hóa đơn trước
            if (hoaDon.CthoaDons.Any())
            {
                _context.CthoaDons.RemoveRange(hoaDon.CthoaDons);
            }

            _context.HoaDons.Remove(hoaDon);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Người dùng {0} xóa hóa đơn: {1}", User.Identity?.Name, id);

            TempData["Success"] = "Xóa hóa đơn thành công";
            return RedirectToAction(nameof(Index));
        }

        private bool HoaDonExists(string id)
        {
            return _context.HoaDons.Any(e => e.MaHoaDon == id);
        }
    }
}
