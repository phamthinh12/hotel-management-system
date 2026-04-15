using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebKhachSan.Models;

namespace WebKhachSan.Controllers
{
    [Authorize]
    public class KhachHangController : Controller
    {
        private readonly QuanLyKhachSanContext _context;
        private readonly ILogger<KhachHangController> _logger;

        public KhachHangController(QuanLyKhachSanContext context, ILogger<KhachHangController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: KhachHang
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Người dùng {0} xem danh sách khách hàng", User.Identity?.Name);
            
            var khachHangs = await _context.KhachHangs
                .Include(k => k.ThuePhongs)
                .Include(k => k.DatPhongs)
                .OrderBy(k => k.MaKhachHang)
                .ToListAsync();

            return View(khachHangs);
        }

        // GET: KhachHang/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var khachHang = await _context.KhachHangs
                .Include(k => k.ThuePhongs)
                    .ThenInclude(tp => tp.CtthuePhongs)
                        .ThenInclude(ct => ct.MaPhongNavigation)
                            .ThenInclude(p => p.MaLoaiPhongNavigation)
                .Include(k => k.DatPhongs)
                .FirstOrDefaultAsync(m => m.MaKhachHang == id);

            if (khachHang == null)
            {
                return NotFound();
            }

            _logger.LogInformation("Người dùng {0} xem chi tiết khách hàng: {1}", User.Identity?.Name, id);

            return View(khachHang);
        }

        // GET: KhachHang/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: KhachHang/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaKhachHang,TenKhachHang,DienThoai,DiaChi,Cccd")] KhachHang khachHang)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra mã khách hàng đã tồn tại
                if (await _context.KhachHangs.AnyAsync(k => k.MaKhachHang == khachHang.MaKhachHang))
                {
                    ModelState.AddModelError("MaKhachHang", "Mã khách hàng đã tồn tại");
                    return View(khachHang);
                }

                // Kiểm tra CCCD
                if (!string.IsNullOrEmpty(khachHang.Cccd))
                {
                    if (await _context.KhachHangs.AnyAsync(k => k.Cccd == khachHang.Cccd))
                    {
                        ModelState.AddModelError("Cccd", "CCCD này đã tồn tại trong hệ thống");
                        return View(khachHang);
                    }
                }

                _context.Add(khachHang);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Người dùng {0} thêm khách hàng mới: {1} - {2}",
                    User.Identity?.Name, khachHang.MaKhachHang, khachHang.TenKhachHang);

                TempData["Success"] = "Thêm khách hàng thành công";
                return RedirectToAction(nameof(Index));
            }

            return View(khachHang);
        }

        // GET: KhachHang/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang == null)
            {
                return NotFound();
            }

            return View(khachHang);
        }

        // POST: KhachHang/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("MaKhachHang,TenKhachHang,DienThoai,DiaChi,Cccd")] KhachHang khachHang)
        {
            if (id != khachHang.MaKhachHang)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Kiểm tra CCCD
                if (!string.IsNullOrEmpty(khachHang.Cccd))
                {
                    if (await _context.KhachHangs.AnyAsync(k => k.Cccd == khachHang.Cccd && k.MaKhachHang != id))
                    {
                        ModelState.AddModelError("Cccd", "CCCD này đã tồn tại trong hệ thống");
                        return View(khachHang);
                    }
                }

                try
                {
                    _context.Update(khachHang);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Người dùng {0} chỉnh sửa khách hàng: {1}",
                        User.Identity?.Name, id);

                    TempData["Success"] = "Cập nhật khách hàng thành công";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await KhachHangExists(khachHang.MaKhachHang))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(khachHang);
        }

        // GET: KhachHang/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var khachHang = await _context.KhachHangs
                .Include(k => k.ThuePhongs)
                .Include(k => k.DatPhongs)
                .FirstOrDefaultAsync(m => m.MaKhachHang == id);

            if (khachHang == null)
            {
                return NotFound();
            }

            return View(khachHang);
        }

        // POST: KhachHang/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang == null)
            {
                return NotFound();
            }

            // Kiểm tra có đơn đặt phòng hoặc thuê phòng
            if (khachHang.DatPhongs.Any() || khachHang.ThuePhongs.Any())
            {
                TempData["Error"] = "Không thể xóa khách hàng này vì có đơn đặt phòng hoặc lịch sử thuê phòng";
                return RedirectToAction(nameof(Delete), new { id = id });
            }

            _context.KhachHangs.Remove(khachHang);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Người dùng {0} xóa khách hàng: {1}",
                User.Identity?.Name, id);

            TempData["Success"] = "Xóa khách hàng thành công";
            return RedirectToAction(nameof(Index));
        }

        // API: Get Customers List
        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            var customers = await _context.KhachHangs
                .OrderBy(k => k.TenKhachHang)
                .Select(k => new
                {
                    maKhachHang = k.MaKhachHang,
                    tenKhachHang = k.TenKhachHang,
                    dienthoai = k.DienThoai,
                    diachi = k.DiaChi
                })
                .ToListAsync();

            return Json(customers);
        }

        // API: Create Customer (Public - for public booking)
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.TenKhachHang) || string.IsNullOrEmpty(request.DienThoai))
                {
                    return Json(new { success = false, message = "Thông tin không hợp lệ" });
                }

                // Generate customer ID
                var lastCustomer = await _context.KhachHangs
                    .OrderByDescending(k => k.MaKhachHang)
                    .FirstOrDefaultAsync();

                int nextNumber = 1;
                if (lastCustomer != null && !string.IsNullOrEmpty(lastCustomer.MaKhachHang))
                {
                    var lastNum = int.TryParse(lastCustomer.MaKhachHang.Replace("KH", ""), out int num) ? num : 0;
                    nextNumber = lastNum + 1;
                }

                string maKhachHang = "KH" + nextNumber.ToString("D6");

                // Check phone already exists
                if (await _context.KhachHangs.AnyAsync(k => k.DienThoai == request.DienThoai))
                {
                    var existing = await _context.KhachHangs
                        .FirstOrDefaultAsync(k => k.DienThoai == request.DienThoai);
                    return Json(new { success = true, maKhachHang = existing.MaKhachHang, message = "Sử dụng khách hàng hiện có" });
                }

                // Create customer
                var khachHang = new KhachHang
                {
                    MaKhachHang = maKhachHang,
                    TenKhachHang = request.TenKhachHang,
                    DienThoai = request.DienThoai,
                    DiaChi = request.DiaChi
                };

                _context.Add(khachHang);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Tạo khách hàng mới từ booking công khai: {0} - {1}", maKhachHang, request.TenKhachHang);

                return Json(new { success = true, maKhachHang = maKhachHang });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo khách hàng");
                return Json(new { success = false, message = "Lỗi máy chủ" });
            }
        }

        private async Task<bool> KhachHangExists(string id)
        {
            return await _context.KhachHangs.AnyAsync(e => e.MaKhachHang == id);
        }
    }

    public class CreateCustomerRequest
    {
        public string TenKhachHang { get; set; }
        public string DienThoai { get; set; }
        public string DiaChi { get; set; }
    }
}
