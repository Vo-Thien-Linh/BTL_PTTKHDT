using BTL_PTTKHDT.Models;
using BTL_PTTKHDT.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BTL_PTTKHDT.Controllers;

public class LoanController : Controller
{
    private readonly QltdnhContext _db;
    private readonly ICreditScoreService _creditScoreService;

    public LoanController(QltdnhContext db, ICreditScoreService creditScoreService)
    {
        _db = db;
        _creditScoreService = creditScoreService;
    }

    private const int PageSize = 10;

    public async Task<IActionResult> Index(string? q, string? status, string? period, int page = 1, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;

        IQueryable<DonVay> baseQuery = _db.DonVays
            .AsNoTracking()
            .Include(x => x.MaKhNavigation)
            .Include(x => x.QuyTrinhPheDuyets);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            baseQuery = baseQuery.Where(x =>
                x.MaDon.Contains(term) ||
                x.MucDichVay.Contains(term) ||
                x.MaKhNavigation.HoTen.Contains(term));
        }

        if (status is "dang-soan")
            baseQuery = baseQuery.Where(x => x.TrangThaiDon == "Đang soạn");
        else if (status is "da-duyet")
            baseQuery = baseQuery.Where(x => x.TrangThaiDon == "Đã duyệt");
        else if (status is "tu-choi")
            baseQuery = baseQuery.Where(x => x.TrangThaiDon == "Từ chối");

        var allForFilter = await baseQuery
            .OrderByDescending(x => x.NgayTao)
            .ToListAsync(cancellationToken);

        IEnumerable<DonVay> filtered = allForFilter;
        if (status is "cho-checker" or "cho-approver")
        {
            filtered = filtered.Where(x =>
            {
                var (maker, checker, approver) = ComputeApprovalStates(x.QuyTrinhPheDuyets);
                var stage = ComputeStageSlug(x.TrangThaiDon, maker, checker, approver);
                return stage == status;
            });
        }

        var total = filtered.Count();
        var items = filtered
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(MapToRowViewModel)
            .ToList();

        var vm = new LoanListViewModel
        {
            Items = items,
            Query = q,
            Status = status,
            Period = period,
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        };

        return View(vm);
    }

    public IActionResult Create()
    {
        return View(new LoanCreateViewModel
        {
            LoaiKhachHang = "personal",
            KyHanDeNghi = 12,
            LaiSuatDeNghi = 12
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LoanCreateViewModel model, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var actor = await GetDefaultEmployeeIdAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(actor))
        {
            ModelState.AddModelError(string.Empty, "Chưa có nhân viên để gán cho hồ sơ (Maker).");
        }

        var maKh = model.MaKh?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(maKh))
        {
            ModelState.AddModelError(nameof(LoanCreateViewModel.MaKh), "Mã khách hàng không được để trống.");
        }

        var customer = string.IsNullOrWhiteSpace(maKh)
            ? null
            : await _db.KhachHangs.AsNoTracking().FirstOrDefaultAsync(x => x.MaKh == maKh, cancellationToken);

        if (customer == null)
        {
            ModelState.AddModelError(nameof(LoanCreateViewModel.MaKh), "Không tìm thấy khách hàng theo mã này.");
        }
        else
        {
            model.TenKhachHang = customer.HoTen;
            model.SoGiayTo = customer.CmndCccd;
            model.LoaiKhachHang = MapCustomerTypeKind(customer.LoaiKhachHang);
            ModelState.Remove(nameof(LoanCreateViewModel.TenKhachHang));
            ModelState.Remove(nameof(LoanCreateViewModel.SoGiayTo));
            ModelState.Remove(nameof(LoanCreateViewModel.LoaiKhachHang));

            var activeCustomerLoan = await GetActiveCustomerLoanAsync(customer.MaKh, exceptLoanApplicationId: null, cancellationToken);
            if (activeCustomerLoan != null)
            {
                ModelState.AddModelError(
                    nameof(LoanCreateViewModel.MaKh),
                    $"Khách hàng đang có khoản vay {activeCustomerLoan.MaVay} chưa tất toán, dư nợ gốc {FormatMoney(activeCustomerLoan.DuNoGoc)}. Chỉ có thể tạo đơn vay mới sau khi khách hàng trả xong khoản vay hiện tại.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var maDon = await GetNextLoanCodeAsync(cancellationToken);

        var newDon = new DonVay
        {
            MaDon = maDon,
            MaKh = customer!.MaKh,
            MaNvsoan = actor,
            MucDichVay = model.MucDichVay.Trim(),
            SoTienYeuCau = model.SoTienYeuCau,
            KyHanDeNghi = model.KyHanDeNghi,
            LaiSuatDeNghi = model.LaiSuatDeNghi,
            NgayNopDon = DateOnly.FromDateTime(now),
            TrangThaiDon = "Chờ duyệt",
            GhiChu = model.GhiChu,
            NgayTao = now,
            NgayCapNhat = now
        };

        newDon.QuyTrinhPheDuyets.Add(new QuyTrinhPheDuyet
        {
            MaPheDuyet = await GetNextPheDuyetCodeAsync(cancellationToken),
            MaDon = maDon,
            MaNv = actor!,
            CapPheDuyet = 1,
            TrangThai = "Đã duyệt",
            NgayXuLy = now,
            GhiChu = null
        });

        _db.DonVays.Add(newDon);
        await _db.SaveChangesAsync(cancellationToken);
        await _creditScoreService.RecalculateAsync(customer.MaKh, "Tạo đơn vay", cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string? id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var don = await _db.DonVays
            .AsNoTracking()
            .Include(x => x.MaKhNavigation)
            .FirstOrDefaultAsync(x => x.MaDon == id, cancellationToken);

        if (don == null) return NotFound();

        var model = new LoanCreateViewModel
        {
            MaKh = don.MaKh,
            TenKhachHang = don.MaKhNavigation.HoTen,
            LoaiKhachHang = MapCustomerTypeKind(don.MaKhNavigation.LoaiKhachHang),
            SoGiayTo = don.MaKhNavigation.CmndCccd,
            SoTienYeuCau = don.SoTienYeuCau,
            KyHanDeNghi = don.KyHanDeNghi,
            LaiSuatDeNghi = don.LaiSuatDeNghi,
            MucDichVay = don.MucDichVay,
            GhiChu = don.GhiChu
        };

        ViewData["MaDon"] = don.MaDon;
        ViewData["TrangThaiDon"] = don.TrangThaiDon;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, LoanCreateViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var don = await _db.DonVays.FirstOrDefaultAsync(x => x.MaDon == id, cancellationToken);
        if (don == null) return NotFound();

        var hasDisbursedLoan = await _db.KhoanVays.AsNoTracking().AnyAsync(x => x.MaDon == id, cancellationToken);
        if (hasDisbursedLoan)
        {
            ModelState.AddModelError(string.Empty, "Don vay da giai ngan khong the sua thong tin.");
        }

        var customer = await _db.KhachHangs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaKh == don.MaKh, cancellationToken);

        if (customer == null)
        {
            ModelState.AddModelError(string.Empty, "Khong tim thay khach hang cua don vay.");
        }
        else
        {
            model.MaKh = customer.MaKh;
            model.TenKhachHang = customer.HoTen;
            model.SoGiayTo = customer.CmndCccd;
            model.LoaiKhachHang = MapCustomerTypeKind(customer.LoaiKhachHang);

            ModelState.Remove(nameof(LoanCreateViewModel.MaKh));
            ModelState.Remove(nameof(LoanCreateViewModel.TenKhachHang));
            ModelState.Remove(nameof(LoanCreateViewModel.SoGiayTo));
            ModelState.Remove(nameof(LoanCreateViewModel.LoaiKhachHang));
        }

        if (!ModelState.IsValid)
        {
            ViewData["MaDon"] = id;
            ViewData["TrangThaiDon"] = don.TrangThaiDon;
            return View(model);
        }

        don.SoTienYeuCau = model.SoTienYeuCau;
        don.KyHanDeNghi = model.KyHanDeNghi;
        don.LaiSuatDeNghi = model.LaiSuatDeNghi;
        don.MucDichVay = model.MucDichVay.Trim();
        don.GhiChu = model.GhiChu;
        don.NgayCapNhat = DateTime.Now;

        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(string? id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var vm = await BuildLoanDetailViewModelAsync(id, cancellationToken);
        if (vm == null) return NotFound();

        ViewData["CollateralCreate"] = new LoanCollateralCreateViewModel();
        ViewData["CollateralValuation"] = new LoanCollateralValuationViewModel();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> AppraisalPdf(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var vm = await BuildLoanDetailViewModelAsync(id, cancellationToken);
        if (vm == null) return NotFound();

        var bytes = LoanAppraisalPdfService.Build(vm);
        var fileName = $"Bao-cao-tham-dinh-{vm.Loan.MaDon}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    private async Task<LoanDetailViewModel?> BuildLoanDetailViewModelAsync(string id, CancellationToken cancellationToken)
    {
        var don = await _db.DonVays
            .AsNoTracking()
            .Include(x => x.MaKhNavigation)
            .Include(x => x.QuyTrinhPheDuyets)
            .FirstOrDefaultAsync(x => x.MaDon == id, cancellationToken);

        if (don == null) return null;

        var taiSan = await _db.TaiSanKhachHangs
            .AsNoTracking()
            .Where(x => x.MaKh == don.MaKh && x.TrangThaiSoHuu == "Đang sở hữu")
            .OrderByDescending(x => x.GiaTriDinhGia ?? x.GiaTriKhaiBao)
            .ToListAsync(cancellationToken);

        var loanRow = MapToRowViewModel(don);

        var taiSanVm = taiSan.Select(x => new LoanCollateralViewModel
        {
            MaTaiSanKh = x.MaTaiSanKh,
            LoaiTaiSan = x.LoaiTaiSan,
            GiaTriKhaiBao = x.GiaTriKhaiBao,
            GiaTriDinhGia = x.GiaTriDinhGia,
            TyLeLtv = x.TyLeLtv,
            TrangThai = x.TrangThai,
            TrangThaiSoHuu = x.TrangThaiSoHuu,
            MoTa = x.MoTa,
            GiayToPhapLy = x.GiayToPhapLy,
            NgayKhaiBao = x.NgayKhaiBao,
            NgayDinhGia = x.NgayDinhGia
        }).ToList();

        var (tongGiaTri, hanMuc) = CalculateCollateralKpis(taiSanVm);
        var ltv = tongGiaTri <= 0 ? 0m : loanRow.SoTienYeuCau / tongGiaTri;
        var steps = BuildApprovalSteps(don.QuyTrinhPheDuyets, loanRow);
        var appraisal = await BuildAppraisalReportAsync(don, loanRow, tongGiaTri, hanMuc, ltv, cancellationToken);

        return new LoanDetailViewModel
        {
            Loan = loanRow,
            ThamDinh = appraisal,
            KyHanDeNghi = don.KyHanDeNghi,
            LaiSuatDeNghi = don.LaiSuatDeNghi,
            NgayNopDon = don.NgayNopDon,
            GhiChu = don.GhiChu,
            TaiSanDamBao = taiSanVm,
            PheDuyet = steps,
            TongGiaTriDamBao = tongGiaTri,
            HanMucGoiY = hanMuc,
            TyLeLtv = ltv
        };
    }

    private async Task<LoanAppraisalReportViewModel> BuildAppraisalReportAsync(
        DonVay don,
        LoanRowViewModel loanRow,
        decimal tongGiaTriDamBao,
        decimal hanMucGoiY,
        decimal tyLeLtv,
        CancellationToken cancellationToken)
    {
        var customer = don.MaKhNavigation;

        var latestCredit = await _db.LichSuTinDungs
            .AsNoTracking()
            .Where(x => x.MaKh == don.MaKh)
            .OrderByDescending(x => x.NgayCapNhat)
            .FirstOrDefaultAsync(cancellationToken);

        var creditLimit = await _db.HanMucTinDungs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaKh == don.MaKh, cancellationToken);

        var activeLoans = await _db.KhoanVays
            .AsNoTracking()
            .Where(x =>
                x.MaKh == don.MaKh
                && x.DuNoGoc > 0
                && x.TrangThai != "Đã trả hết"
                && x.TrangThai != "Xóa nợ")
            .ToListAsync(cancellationToken);

        var tongDuNo = activeLoans.Sum(x => x.DuNoGoc);
        var nhomNoCaoNhat = activeLoans.Select(x => x.NhomNo).DefaultIfEmpty((byte)1).Max();
        var coNoQuaHan = activeLoans.Any(x => x.TrangThai == "Quá hạn" || x.NhomNo >= 2);

        return new LoanAppraisalReportViewModel
        {
            MaKh = customer.MaKh,
            HoTen = customer.HoTen,
            LoaiKhachHang = customer.LoaiKhachHang,
            NgaySinh = customer.NgaySinh,
            CmndCccd = customer.CmndCccd,
            DiaChi = customer.DiaChi,
            SoDienThoai = customer.SoDienThoai,
            Email = customer.Email,
            IsActive = customer.IsActive,
            MaSoThue = customer.MaSoThue,
            TenNguoiDaiDien = customer.TenNguoiDaiDien,
            ChucVuNguoiDaiDien = customer.ChucVuNguoiDaiDien,
            NgayThanhLap = customer.NgayThanhLap,
            LinhVucKinhDoanh = customer.LinhVucKinhDoanh,
            DoanhThuBinhQuanThang = customer.DoanhThuBinhQuanThang,
            LoiNhuanBinhQuanThang = customer.LoiNhuanBinhQuanThang,
            SoLaoDong = customer.SoLaoDong,
            DiemTinDung = latestCredit?.DiemTinDung,
            XepHangRuiRo = latestCredit?.XepHangRuiRo,
            SoLanTraTre = latestCredit?.SoLanTraTre ?? 0,
            ThuNhapHangThang = latestCredit?.ThuNhapHangThang,
            TyLeNoThuNhap = latestCredit?.TyLeNoThuNhap,
            GhiChuTinDung = latestCredit?.GhiChu,
            NgayCapNhatTinDung = latestCredit?.NgayCapNhat,
            NguonCapNhatTinDung = latestCredit?.NguonCapNhat,
            TongDuNoGocHienTai = tongDuNo,
            SoKhoanVayDangHoatDong = activeLoans.Count,
            NhomNoCaoNhat = nhomNoCaoNhat,
            CoNoQuaHan = coNoQuaHan,
            CoNoXau = activeLoans.Any(x => x.NhomNo >= 3),
            SoTienYeuCau = loanRow.SoTienYeuCau,
            TongGiaTriDamBao = tongGiaTriDamBao,
            HanMucGoiY = hanMucGoiY,
            TyLeLtv = tyLeLtv,
            HanMucToiDa = creditLimit?.HanMucToiDa,
            HanMucDaSuDung = creditLimit?.HanMucDaSuDung,
            HanMucConLai = creditLimit?.HanMucConLai,
            NgayCapNhatHanMuc = creditLimit?.NgayCapNhat
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCollateral(string id, LoanCollateralCreateViewModel model, CancellationToken cancellationToken = default)
    {
        var don = await _db.DonVays.AsNoTracking().FirstOrDefaultAsync(x => x.MaDon == id, cancellationToken);
        if (don == null) return NotFound();

        var loaiTaiSanRaw = (Request.Form["LoaiTaiSan"].ToString() ?? model.LoaiTaiSan ?? string.Empty).Trim();
        var giaTriKhaiBaoRaw = Request.Form["GiaTriKhaiBao"].ToString();
        var tyLeLtvRaw = Request.Form["TyLeLtv"].ToString();
        var giayToPhapLyRaw = Request.Form["GiayToPhapLy"].ToString();
        var moTaRaw = Request.Form["MoTa"].ToString();

        if (string.IsNullOrWhiteSpace(loaiTaiSanRaw))
        {
            TempData["CollateralError"] = "Loai tai san không được để trống.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (loaiTaiSanRaw.Length > 100)
        {
            TempData["CollateralError"] = "Loai tai san không được vượt quá 100 ký tự.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!TryParseFlexibleDecimal(giaTriKhaiBaoRaw, out var giaTriKhaiBao) || giaTriKhaiBao <= 0)
        {
            TempData["CollateralError"] = "Giá trị khai báo phải là số và lớn hơn 0.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!TryParseFlexibleDouble(tyLeLtvRaw, out var tyLeLtv) || tyLeLtv <= 0 || tyLeLtv > 1)
        {
            TempData["CollateralError"] = "Tỷ lệ LTV phải là số trong khoảng (0, 1].";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!string.IsNullOrWhiteSpace(giayToPhapLyRaw) && giayToPhapLyRaw.Length > 500)
        {
            TempData["CollateralError"] = "Giay to phap ly không được vượt quá 500 ký tự.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!string.IsNullOrWhiteSpace(moTaRaw) && moTaRaw.Length > 500)
        {
            TempData["CollateralError"] = "Mô tả không được vượt quá 500 ký tự.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var newCode = await GetNextTaiSanKhCodeAsync(cancellationToken);
        var entity = new TaiSanKhachHang
        {
            MaTaiSanKh = newCode,
            MaKh = don.MaKh,
            LoaiTaiSan = loaiTaiSanRaw,
            GiaTriKhaiBao = giaTriKhaiBao,
            GiaTriDinhGia = null,
            TyLeLtv = tyLeLtv,
            GiayToPhapLy = string.IsNullOrWhiteSpace(giayToPhapLyRaw) ? null : giayToPhapLyRaw,
            MoTa = string.IsNullOrWhiteSpace(moTaRaw) ? null : moTaRaw,
            NgayKhaiBao = DateOnly.FromDateTime(DateTime.Now),
            NgayDinhGia = null,
            MaNvdinhGia = null,
            TrangThai = "Chưa định giá",
            TrangThaiSoHuu = "Đang sở hữu",
            NgayBan = null,
            GhiChuSoHuu = null
        };

        _db.TaiSanKhachHangs.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValuateCollateral(string id, LoanCollateralValuationViewModel model, CancellationToken cancellationToken = default)
    {
        var don = await _db.DonVays.AsNoTracking().FirstOrDefaultAsync(x => x.MaDon == id, cancellationToken);
        if (don == null) return NotFound();

        var maTaiSanKhRaw = (Request.Form["MaTaiSanKh"].ToString() ?? model.MaTaiSanKh ?? string.Empty).Trim();
        var giaTriDinhGiaRaw = Request.Form["GiaTriDinhGia"].ToString();

        if (string.IsNullOrWhiteSpace(maTaiSanKhRaw))
        {
            TempData["CollateralError"] = "Thiếu mã tài sản cần định giá.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!TryParseFlexibleDecimal(giaTriDinhGiaRaw, out var giaTriDinhGia) || giaTriDinhGia <= 0)
        {
            TempData["CollateralError"] = "Giá trị định giá phải là số và lớn hơn 0.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var asset = await _db.TaiSanKhachHangs.FirstOrDefaultAsync(x => x.MaTaiSanKh == maTaiSanKhRaw && x.MaKh == don.MaKh, cancellationToken);
        if (asset == null) return RedirectToAction(nameof(Details), new { id });

        var actor = await GetDefaultEmployeeIdAsync(cancellationToken);
        asset.GiaTriDinhGia = giaTriDinhGia;
        asset.NgayDinhGia = DateOnly.FromDateTime(DateTime.Now);
        asset.TrangThai = "Đã định giá";
        asset.MaNvdinhGia = actor;

        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCollateral(string id, CancellationToken cancellationToken = default)
    {
        var don = await _db.DonVays.AsNoTracking().FirstOrDefaultAsync(x => x.MaDon == id, cancellationToken);
        if (don == null) return NotFound();

        var maTaiSanKh = Request.Form["MaTaiSanKh"].ToString().Trim();
        var asset = await _db.TaiSanKhachHangs.FirstOrDefaultAsync(x => x.MaTaiSanKh == maTaiSanKh && x.MaKh == don.MaKh, cancellationToken);
        if (asset == null) return RedirectToAction(nameof(Details), new { id });

        if (await IsCollateralPledgedAsync(maTaiSanKh, cancellationToken))
        {
            TempData["CollateralError"] = "Tài sản đã được thế chấp cho khoản vay, không thể sửa trực tiếp.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var loaiTaiSan = Request.Form["LoaiTaiSan"].ToString().Trim();
        var giaTriKhaiBaoRaw = Request.Form["GiaTriKhaiBao"].ToString();
        var tyLeLtvRaw = Request.Form["TyLeLtv"].ToString();
        var giayToPhapLy = Request.Form["GiayToPhapLy"].ToString().Trim();
        var moTa = Request.Form["MoTa"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(loaiTaiSan))
        {
            TempData["CollateralError"] = "Loai tai san không được để trống.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (loaiTaiSan.Length > 100)
        {
            TempData["CollateralError"] = "Loai tai san không được vượt quá 100 ký tự.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!TryParseFlexibleDecimal(giaTriKhaiBaoRaw, out var giaTriKhaiBao) || giaTriKhaiBao <= 0)
        {
            TempData["CollateralError"] = "Giá trị khai báo phải là số và lớn hơn 0.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!TryParseFlexibleDouble(tyLeLtvRaw, out var tyLeLtv) || tyLeLtv <= 0 || tyLeLtv > 1)
        {
            TempData["CollateralError"] = "Tỷ lệ LTV phải là số trong khoảng (0, 1].";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (giayToPhapLy.Length > 500 || moTa.Length > 500)
        {
            TempData["CollateralError"] = "Giay to phap ly hoặc mô tả không được vượt quá 500 ký tự.";
            return RedirectToAction(nameof(Details), new { id });
        }

        asset.LoaiTaiSan = loaiTaiSan;
        asset.GiaTriKhaiBao = giaTriKhaiBao;
        asset.TyLeLtv = tyLeLtv;
        asset.GiayToPhapLy = string.IsNullOrWhiteSpace(giayToPhapLy) ? null : giayToPhapLy;
        asset.MoTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa;

        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCollateral(string id, string maTaiSanKh, CancellationToken cancellationToken = default)
    {
        var don = await _db.DonVays.AsNoTracking().FirstOrDefaultAsync(x => x.MaDon == id, cancellationToken);
        if (don == null) return NotFound();

        var asset = await _db.TaiSanKhachHangs.FirstOrDefaultAsync(x => x.MaTaiSanKh == maTaiSanKh && x.MaKh == don.MaKh, cancellationToken);
        if (asset == null) return RedirectToAction(nameof(Details), new { id });

        if (await IsCollateralPledgedAsync(maTaiSanKh, cancellationToken))
        {
            TempData["CollateralError"] = "Tài sản đã được thế chấp cho khoản vay, không thể xóa. Cần xử lý giải chấp trước.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var hasCollateralHistory = await _db.TaiSanTheChaps
            .AsNoTracking()
            .AnyAsync(x => x.MaTaiSanKh == maTaiSanKh, cancellationToken);
        if (hasCollateralHistory)
        {
            TempData["CollateralError"] = "Tài sản đã có lịch sử thế chấp nên không thể xóa khỏi hệ thống. Hãy cập nhật trạng thái sở hữu ở hồ sơ khách hàng nếu tài sản không còn thuộc khách hàng.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            _db.TaiSanKhachHangs.Remove(asset);
            await _db.SaveChangesAsync(cancellationToken);
            TempData["CollateralSuccess"] = "Da xoa tai san khoi ho so.";
        }
        catch (DbUpdateException)
        {
            TempData["CollateralError"] = "Khong the xoa tai san vi dang duoc tham chieu boi du lieu nghiep vu.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<bool> IsCollateralPledgedAsync(string maTaiSanKh, CancellationToken cancellationToken)
    {
        return await _db.TaiSanTheChaps
            .AsNoTracking()
            .AnyAsync(x =>
                x.MaTaiSanKh == maTaiSanKh
                && (x.TrangThai == "Đang thế chấp" || x.TrangThai == "Xử lý"),
                cancellationToken);
    }

    private static bool TryParseFlexibleDecimal(string? input, out decimal value)
    {
        value = 0m;
        var s = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(s)) return false;

        s = s.Replace("₫", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty);

        s = new string(s.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());
        if (string.IsNullOrWhiteSpace(s)) return false;

        var dotCount = s.Count(c => c == '.');
        var commaCount = s.Count(c => c == ',');

        if (dotCount > 0 && commaCount > 0)
        {
            s = s.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (dotCount > 1 && commaCount == 0)
        {
            s = s.Replace(".", string.Empty);
        }
        else if (commaCount > 1 && dotCount == 0)
        {
            s = s.Replace(",", string.Empty);
        }
        else if (commaCount == 1 && dotCount == 0)
        {
            var parts = s.Split(',');
            s = parts.Length == 2 && parts[1].Length == 3 ? string.Concat(parts[0], parts[1]) : string.Concat(parts[0], ".", parts.ElementAtOrDefault(1));
        }

        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseFlexibleDouble(string? input, out double value)
    {
        value = 0d;
        if (!TryParseFlexibleDecimal(input, out var d)) return false;
        value = (double)d;
        return true;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(string id, int cap, string decision, string? note, CancellationToken cancellationToken = default)
    {
        var don = await _db.DonVays
            .Include(x => x.QuyTrinhPheDuyets)
            .FirstOrDefaultAsync(x => x.MaDon == id, cancellationToken);
        if (don == null) return NotFound();

        if (cap is < 1 or > 3) return BadRequest();
        var normalizedDecision = (decision ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedDecision is not ("approve" or "reject")) return BadRequest();

        var now = DateTime.Now;
        var actor = await GetDefaultEmployeeIdAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(actor)) return RedirectToAction(nameof(Details), new { id });

        var existing = don.QuyTrinhPheDuyets.FirstOrDefault(x => x.CapPheDuyet == (byte)cap);
        if (existing == null)
        {
            existing = new QuyTrinhPheDuyet
            {
                MaPheDuyet = await GetNextPheDuyetCodeAsync(cancellationToken),
                MaDon = don.MaDon,
                MaNv = actor,
                CapPheDuyet = (byte)cap,
                TrangThai = "Chờ duyệt",
                NgayXuLy = now,
                GhiChu = note
            };
            don.QuyTrinhPheDuyets.Add(existing);
        }

        if (normalizedDecision == "reject")
        {
            existing.MaNv = actor;
            existing.TrangThai = "Từ chối";
            existing.NgayXuLy = now;
            existing.GhiChu = note;
            don.TrangThaiDon = "Từ chối";
            don.NgayCapNhat = now;
            await _db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        existing.MaNv = actor;
        existing.TrangThai = "Đã duyệt";
        existing.NgayXuLy = now;
        existing.GhiChu = note;

        if (cap == 1)
        {
            don.TrangThaiDon = "Chờ duyệt";
            don.NgayCapNhat = now;
            await _db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        if (cap == 2)
        {
            don.TrangThaiDon = "Chờ duyệt";
            don.NgayCapNhat = now;
            await _db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        don.TrangThaiDon = "Đã duyệt";
        don.NgayCapNhat = now;
        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    private static (decimal tongGiaTri, decimal hanMuc) CalculateCollateralKpis(IEnumerable<LoanCollateralViewModel> list)
    {
        decimal tong = 0;
        decimal hanMuc = 0;
        foreach (var item in list)
        {
            var baseValue = item.GiaTriDinhGia ?? item.GiaTriKhaiBao;
            tong += baseValue;
            hanMuc += baseValue * (decimal)item.TyLeLtv;
        }
        return (tong, hanMuc);
    }

    private static IReadOnlyList<LoanApprovalStepViewModel> BuildApprovalSteps(IEnumerable<QuyTrinhPheDuyet> records, LoanRowViewModel row)
    {
        LoanApprovalStepViewModel MakeDefault(int cap, string capText, string state)
        {
            var t = state switch
            {
                "approved" => "Đã duyệt",
                "rejected" => "Từ chối",
                "pending" => "Chờ duyệt",
                _ => "Chưa kích hoạt"
            };
            return new LoanApprovalStepViewModel { CapPheDuyet = cap, CapText = capText, TrangThai = t };
        }

        LoanApprovalStepViewModel MakeFromRecord(QuyTrinhPheDuyet r, string capText)
        {
            var t = r.TrangThai switch
            {
                "Đã duyệt" => "Đã duyệt",
                "Từ chối" => "Từ chối",
                _ => "Chờ duyệt"
            };

            return new LoanApprovalStepViewModel
            {
                CapPheDuyet = r.CapPheDuyet,
                CapText = capText,
                TrangThai = t,
                MaNv = r.MaNv,
                NgayXuLy = r.NgayXuLy,
                GhiChu = r.GhiChu
            };
        }

        var r1 = records.FirstOrDefault(x => x.CapPheDuyet == 1);
        var r2 = records.FirstOrDefault(x => x.CapPheDuyet == 2);
        var r3 = records.FirstOrDefault(x => x.CapPheDuyet == 3);

        return new[]
        {
            r1 != null ? MakeFromRecord(r1, "Tạo đơn") : MakeDefault(1, "Tạo đơn", row.TrangThaiMaker),
            r2 != null ? MakeFromRecord(r2, "Kiểm tra") : MakeDefault(2, "Kiểm tra", row.TrangThaiChecker),
            r3 != null ? MakeFromRecord(r3, "Phê duyệt") : MakeDefault(3, "Phê duyệt", row.TrangThaiApprover)
        };
    }

    private static (string maker, string checker, string approver) ComputeApprovalStates(IEnumerable<QuyTrinhPheDuyet> records)
    {
        string Map(QuyTrinhPheDuyet? r)
        {
            if (r == null) return "missing";
            return r.TrangThai switch
            {
                "Đã duyệt" => "approved",
                "Từ chối" => "rejected",
                _ => "pending"
            };
        }

        var maker = Map(records.FirstOrDefault(x => x.CapPheDuyet == 1));
        var checker = Map(records.FirstOrDefault(x => x.CapPheDuyet == 2));
        var approver = Map(records.FirstOrDefault(x => x.CapPheDuyet == 3));
        return (maker, checker, approver);
    }

    private static string ComputeStageSlug(string trangThaiDonDb, string maker, string checker, string approver)
    {
        if (trangThaiDonDb == "Đã duyệt") return "da-duyet";
        if (trangThaiDonDb == "Từ chối") return "tu-choi";
        if (trangThaiDonDb == "Đã hủy") return "da-huy";
        if (trangThaiDonDb == "Đang soạn") return "dang-soan";

        if (maker == "approved" && checker != "approved") return "cho-checker";
        if (maker == "approved" && checker == "approved" && approver != "approved") return "cho-approver";
        return "cho-checker";
    }

    private static LoanRowViewModel MapToRowViewModel(DonVay don)
    {
        var kind = MapCustomerTypeKind(don.MaKhNavigation.LoaiKhachHang);
        var nhanDang = kind == "business" ? "MST:" : "CMND:";
        var (maker, checker, approver) = ComputeApprovalStates(don.QuyTrinhPheDuyets);

        var makerState = maker == "missing" ? "pending" : maker;
        var checkerState = checker == "missing"
            ? makerState == "approved" ? "pending" : "inactive"
            : checker;
        var approverState = approver == "missing"
            ? checkerState == "approved" ? "pending" : "inactive"
            : approver;

        var stageSlug = ComputeStageSlug(don.TrangThaiDon, makerState, checkerState, approverState);
        if (don.TrangThaiDon == "Từ chối")
        {
            if (makerState != "rejected" && checkerState != "rejected" && approverState != "rejected")
            {
                approverState = "rejected";
            }
        }

        return new LoanRowViewModel
        {
            MaDon = don.MaDon,
            MaKh = don.MaKh,
            TenKhachHang = don.MaKhNavigation.HoTen,
            LoaiKhachHang = kind,
            SoGiayTo = don.MaKhNavigation.CmndCccd,
            NhanDangGiayTo = nhanDang,
            SoTienYeuCau = don.SoTienYeuCau,
            MucDichVay = don.MucDichVay,
            TrangThaiDon = stageSlug,
            TrangThaiMaker = makerState,
            TrangThaiChecker = checkerState,
            TrangThaiApprover = approverState
        };
    }

    private static string MapCustomerTypeKind(string raw)
    {
        var s = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        return s.Contains("doanh") ? "business" : "personal";
    }

    private static int ParseCodeSuffix(string code, string prefix)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return 0;

        var suffix = code.Substring(prefix.Length);
        return int.TryParse(suffix, out var value) ? value : 0;
    }

    private async Task<string> GetNextLoanCodeAsync(CancellationToken ct)
    {
        var codes = await _db.DonVays.AsNoTracking().Select(x => x.MaDon).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "DV")).DefaultIfEmpty(0).Max();
        return $"DV{(maxId + 1):0000}";
    }

    private async Task<string> GetNextPheDuyetCodeAsync(CancellationToken ct)
    {
        var codes = await _db.QuyTrinhPheDuyets.AsNoTracking().Select(x => x.MaPheDuyet).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "PD")).DefaultIfEmpty(0).Max();
        return $"PD{(maxId + 1):0000}";
    }

    private async Task<string> GetNextTaiSanKhCodeAsync(CancellationToken ct)
    {
        var codes = await _db.TaiSanKhachHangs.AsNoTracking().Select(x => x.MaTaiSanKh).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "TSK")).DefaultIfEmpty(0).Max();
        return $"TSK{(maxId + 1):0000}";
    }

    private async Task<string> GetNextKhoanVayCodeAsync(CancellationToken ct)
    {
        var codes = await _db.KhoanVays.AsNoTracking().Select(x => x.MaVay).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "KV")).DefaultIfEmpty(0).Max();
        return $"KV{(maxId + 1):0000}";
    }

    private async Task<string> GetNextHopDongCodeAsync(CancellationToken ct)
    {
        var codes = await _db.HopDongTinDungs.AsNoTracking().Select(x => x.MaHopDong).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "HD")).DefaultIfEmpty(0).Max();
        return $"HD{(maxId + 1):0000}";
    }

    private async Task<(int start, int next)> GetNextLichTraNoSuffixAsync(CancellationToken ct)
    {
        var codes = await _db.LichTraNos.AsNoTracking().Select(x => x.MaLichTraNo).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "LTN")).DefaultIfEmpty(0).Max();
        return (maxId, maxId + 1);
    }

    private async Task<(int start, int next)> GetNextTaiSanTheChapSuffixAsync(CancellationToken ct)
    {
        var codes = await _db.TaiSanTheChaps.AsNoTracking().Select(x => x.MaTaiSan).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "TSC")).DefaultIfEmpty(0).Max();
        return (maxId, maxId + 1);
    }

    private sealed record ActiveCustomerLoanInfo(string MaVay, decimal DuNoGoc, string TrangThai);

    private async Task<ActiveCustomerLoanInfo?> GetActiveCustomerLoanAsync(string maKh, string? exceptLoanApplicationId, CancellationToken cancellationToken)
    {
        return await _db.KhoanVays
            .AsNoTracking()
            .Where(x =>
                x.MaKh == maKh
                && (exceptLoanApplicationId == null || x.MaDon != exceptLoanApplicationId)
                && x.DuNoGoc > 0
                && x.TrangThai != "Đã trả hết"
                && x.TrangThai != "Xóa nợ")
            .OrderByDescending(x => x.NgayGiaiNgan)
            .Select(x => new ActiveCustomerLoanInfo(x.MaVay, x.DuNoGoc, x.TrangThai))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Disburse(string id)
    {
        return RedirectToAction("Details", "Disbursement", new { id });
    }

    private static IEnumerable<(int kyThu, DateOnly ngayPhaiTra, decimal goc, decimal lai)> BuildScheduleGocLaiDeu(decimal principal, double annualRatePercent, int termMonths, DateOnly disburseDate)
    {
        if (termMonths <= 0 || principal <= 0) yield break;

        var r = (decimal)(annualRatePercent / 100d / 12d);
        decimal payment;
        if (r <= 0)
        {
            payment = principal / termMonths;
        }
        else
        {
            var pow = (decimal)Math.Pow((double)(1m + r), -termMonths);
            payment = principal * r / (1m - pow);
        }

        var remaining = principal;
        for (var k = 1; k <= termMonths; k++)
        {
            var interest = r <= 0 ? 0m : remaining * r;
            var principalPay = payment - interest;

            if (k == termMonths)
            {
                principalPay = remaining;
                payment = principalPay + interest;
            }

            remaining -= principalPay;
            if (remaining < 0) remaining = 0;

            yield return (
                k,
                disburseDate.AddMonths(k),
                DecimalRoundMoney(principalPay),
                DecimalRoundMoney(interest)
            );
        }
    }

    private static decimal DecimalRoundMoney(decimal value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }

    private async Task<string?> GetDefaultEmployeeIdAsync(CancellationToken ct)
    {
        var nv = await _db.NhanViens.AsNoTracking().OrderBy(x => x.MaNv).Select(x => x.MaNv).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(nv) ? null : nv;
    }

    // ──────────────────────────────────────────────
    // API Endpoints
    // ──────────────────────────────────────────────

    /// <summary>
    /// Tim kiếm và lọc hồ sơ vay — trả JSON cho AJAX.
    /// GET /api/loans/search?q=&amp;status=&amp;page=1&amp;pageSize=10
    /// </summary>
    [HttpGet("/api/loans/search")]
    public async Task<IActionResult> SearchApi(string? q, string? status, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 50) pageSize = 10;

        IQueryable<DonVay> baseQuery = _db.DonVays
            .AsNoTracking()
            .Include(x => x.MaKhNavigation)
            .Include(x => x.QuyTrinhPheDuyets);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            baseQuery = baseQuery.Where(x =>
                x.MaDon.Contains(term) ||
                x.MucDichVay.Contains(term) ||
                x.MaKhNavigation.HoTen.Contains(term));
        }

        if (status is "dang-soan")
            baseQuery = baseQuery.Where(x => x.TrangThaiDon == "Đang soạn");
        else if (status is "da-duyet")
            baseQuery = baseQuery.Where(x => x.TrangThaiDon == "Đã duyệt");
        else if (status is "tu-choi")
            baseQuery = baseQuery.Where(x => x.TrangThaiDon == "Từ chối");

        var allForFilter = await baseQuery
            .OrderByDescending(x => x.NgayTao)
            .ToListAsync(cancellationToken);

        IEnumerable<DonVay> filtered = allForFilter;
        if (status is "cho-checker" or "cho-approver")
        {
            filtered = filtered.Where(x =>
            {
                var (maker, checker, approver) = ComputeApprovalStates(x.QuyTrinhPheDuyets);
                var stage = ComputeStageSlug(x.TrangThaiDon, maker, checker, approver);
                return stage == status;
            });
        }

        var totalCount = filtered.Count();
        var totalPages = totalCount <= 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);
        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapToRowViewModel(x))
            .Select(x => new
            {
                x.MaDon,
                x.MaKh,
                x.TenKhachHang,
                x.LoaiKhachHang,
                x.SoGiayTo,
                x.NhanDangGiayTo,
                SoTienYeuCauText = x.SoTienYeuCau.ToString("N0"),
                x.MucDichVay,
                x.TrangThaiDon,
                x.TrangThaiMaker,
                x.TrangThaiChecker,
                x.TrangThaiApprover
            })
            .ToList();

        return Json(new { items, totalCount, page, totalPages });
    }

    /// <summary>
    /// Lấy chi tiết 1 hồ sơ vay theo mã đơn - trả JSON.
    /// GET /api/loans/{id}/detail
    /// </summary>
    [HttpGet("/api/loans/{id}/detail")]
    public async Task<IActionResult> DetailApi(string id, CancellationToken cancellationToken = default)
    {
        var don = await _db.DonVays
            .AsNoTracking()
            .Include(x => x.MaKhNavigation)
            .Include(x => x.QuyTrinhPheDuyets)
            .FirstOrDefaultAsync(x => x.MaDon == id, cancellationToken);

        if (don == null) return NotFound(new { message = $"Không tìm thấy hồ sơ: {id}" });
        var loan = MapToRowViewModel(don);

        return Json(new
        {
            loan.MaDon,
            loan.MaKh,
            loan.TenKhachHang,
            loan.LoaiKhachHang,
            loan.SoGiayTo,
            loan.NhanDangGiayTo,
            SoTienYeuCauText = loan.SoTienYeuCau.ToString("N0"),
            loan.MucDichVay,
            loan.TrangThaiDon,
            loan.TrangThaiMaker,
            loan.TrangThaiChecker,
            loan.TrangThaiApprover
        });
    }
}
