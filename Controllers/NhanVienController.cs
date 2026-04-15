using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebKhachSan.Models;

namespace WebKhachSan.Controllers
{
    [Authorize]
    public class NhanVienController : Controller
    {
        private readonly QuanLyKhachSanContext _context;
        private readonly ILogger<NhanVienController> _logger;

        public NhanVienController(QuanLyKhachSanContext context, ILogger<NhanVienController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: NhanVien
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Người dùng {0} xem danh sách nhân viên", User.Identity?.Name);
            
            var nhanViens = await _context.NhanViens
                .Include(nv => nv.MaTaiKhoanNavigation)
                .OrderBy(nv => nv.MaNhanVien)
                .ToListAsync();

            return View(nhanViens);
        }

        // GET: NhanVien/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var nhanVien = await _context.NhanViens
                .Include(nv => nv.MaTaiKhoanNavigation)
                .Include(nv => nv.HoaDons)
                .FirstOrDefaultAsync(m => m.MaNhanVien == id);

            if (nhanVien == null)
            {
                return NotFound();
            }

            _logger.LogInformation("Người dùng {0} xem chi tiết nhân viên: {1}", User.Identity?.Name, id);

            return View(nhanVien);
        }

        // GET: NhanVien/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: NhanVien/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaNhanVien,TenNhanVien,NgaySinh,DienThoai,DiaChi,ChucVu,MaTaiKhoan")] NhanVien nhanVien)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra mã nhân viên đã tồn tại
                if (await _context.NhanViens.AnyAsync(nv => nv.MaNhanVien == nhanVien.MaNhanVien))
                {
                    ModelState.AddModelError("MaNhanVien", "Mã nhân viên đã tồn tại");
                    return View(nhanVien);
                }

                _context.Add(nhanVien);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Người dùng {0} thêm nhân viên mới: {1} - {2}",
                    User.Identity?.Name, nhanVien.MaNhanVien, nhanVien.TenNhanVien);

                TempData["Success"] = "Thêm nhân viên thành công";
                return RedirectToAction(nameof(Index));
            }

            return View(nhanVien);
        }

        // GET: NhanVien/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var nhanVien = await _context.NhanViens.FindAsync(id);
            if (nhanVien == null)
            {
                return NotFound();
            }

            return View(nhanVien);
        }

        // POST: NhanVien/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("MaNhanVien,TenNhanVien,NgaySinh,DienThoai,DiaChi,ChucVu,MaTaiKhoan")] NhanVien nhanVien)
        {
            if (id != nhanVien.MaNhanVien)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(nhanVien);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Người dùng {0} cập nhật nhân viên: {1}", 
                        User.Identity?.Name, nhanVien.MaNhanVien);
                    
                    TempData["Success"] = "Cập nhật nhân viên thành công";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!NhanVienExists(nhanVien.MaNhanVien))
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
            return View(nhanVien);
        }

        // GET: NhanVien/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var nhanVien = await _context.NhanViens
                .Include(nv => nv.HoaDons)
                .FirstOrDefaultAsync(m => m.MaNhanVien == id);

            if (nhanVien == null)
            {
                return NotFound();
            }

            return View(nhanVien);
        }

        // POST: NhanVien/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var nhanVien = await _context.NhanViens.FindAsync(id);
            if (nhanVien == null)
            {
                return NotFound();
            }

            // Kiểm tra có hóa đơn liên quan
            if (nhanVien.HoaDons.Any())
            {
                TempData["Error"] = "Không thể xóa nhân viên này vì có liên quan đến hóa đơn";
                return RedirectToAction(nameof(Delete), new { id = id });
            }

            _context.NhanViens.Remove(nhanVien);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Người dùng {0} xóa nhân viên: {1}",
                User.Identity?.Name, id);

            TempData["Success"] = "Xóa nhân viên thành công";
            return RedirectToAction(nameof(Index));
        }

        private bool NhanVienExists(string id)
        {
            return _context.NhanViens.Any(e => e.MaNhanVien == id);
        }
    }
}
