using System.Globalization;
using BTL_PTTKHDT.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace BTL_PTTKHDT.Services;

public static class LoanAppraisalPdfService
{
    public static byte[] Build(LoanDetailViewModel model)
    {
        using var ms = new MemoryStream();
        using var document = new Document(PageSize.A4, 42f, 42f, 30f, 36f);
        PdfWriter.GetInstance(document, ms);
        document.Open();

        var pdf = new PdfBuilder(document);
        var report = model.ThamDinh;
        var approval = model.PheDuyet
            .Where(x => !string.IsNullOrWhiteSpace(x.MaNv) || !string.IsNullOrWhiteSpace(x.GhiChu))
            .OrderByDescending(x => x.CapPheDuyet)
            .FirstOrDefault();

        pdf.Header();
        pdf.Title("BÁO CÁO THẨM ĐỊNH VÀ ĐỀ NGHỊ DUYỆT CHO VAY");

        pdf.Section("I. THÔNG TIN KHÁCH HÀNG");
        pdf.Line($"Mã khách hàng: {report.MaKh}");
        pdf.Line($"Họ tên / Tên doanh nghiệp: {report.HoTen}");
        pdf.Line($"Loại khách hàng: {report.LoaiKhachHang}");
        pdf.Line(CustomerDateLine(report));
        pdf.Line($"CMND/CCCD: {Text(report.CmndCccd)}");
        pdf.Line($"Mã số thuế: {Text(report.MaSoThue)}");
        pdf.Line($"Số điện thoại: {Text(report.SoDienThoai)}");
        pdf.Line($"Email: {Text(report.Email)}");
        pdf.Line($"Địa chỉ: {Text(report.DiaChi)}");
        pdf.Space();
        pdf.Line("Đối với khách hàng doanh nghiệp");
        pdf.Line($"Người đại diện: {Text(report.TenNguoiDaiDien)}");
        pdf.Line($"Chức vụ: {Text(report.ChucVuNguoiDaiDien)}");
        pdf.Line($"Lĩnh vực kinh doanh: {Text(report.LinhVucKinhDoanh)}");
        pdf.Line($"Số lao động: {Text(report.SoLaoDong?.ToString())}");
        pdf.Space();
        pdf.Line("Khách hàng đang hoạt động:");
        pdf.Line($"{Check(report.IsActive)} Có     {Check(!report.IsActive)} Không");
        pdf.Line("Nhận xét tư cách khách hàng:");
        pdf.Line(Dots());
        pdf.Space();

        pdf.Section("II. THÔNG TIN ĐỀ NGHỊ VAY");
        pdf.Line($"Mã đơn vay: {model.Loan.MaDon}");
        pdf.Line($"Ngày nộp đơn: {model.NgayNopDon:dd/MM/yyyy}");
        pdf.Line($"Mục đích vay: {model.Loan.MucDichVay}");
        pdf.Line($"Số tiền yêu cầu vay: {Money(model.Loan.SoTienYeuCau)}");
        pdf.Line($"Kỳ hạn đề nghị: {model.KyHanDeNghi} tháng");
        pdf.Line($"Lãi suất đề nghị: {(model.LaiSuatDeNghi.HasValue ? $"{model.LaiSuatDeNghi:0.##}%/năm" : Blank())}");
        pdf.Line($"Trạng thái hồ sơ: {model.Loan.TrangThaiDon}");
        pdf.Space();
        pdf.Line("Mục đích vay vốn hợp pháp:");
        pdf.Line($"{Check(report.MucDichHopPhap)} Có     {Check(!report.MucDichHopPhap)} Không");
        pdf.Line("Nhận xét mục đích vay:");
        pdf.Line(Dots());
        pdf.Space();

        pdf.Section("III. TÌNH HÌNH TÀI CHÍNH VÀ KHẢ NĂNG TRẢ NỢ");
        pdf.Line("1. Thu nhập / Doanh thu");
        pdf.Line($"Nghề nghiệp: {Text(report.NgheNghiep)}");
        pdf.Line($"Nơi làm việc: {Text(report.NoiLamViec)}");
        pdf.Line($"Chức vụ: {Text(report.ChucVu)}");
        pdf.Line($"Doanh thu bình quân tháng: {Money(report.DoanhThuBinhQuanThang)}");
        pdf.Line($"Lợi nhuận bình quân tháng: {Money(report.LoiNhuanBinhQuanThang)}");
        pdf.Line($"Thu nhập hàng tháng theo lịch sử tín dụng: {Money(report.ThuNhapHangThang)}");
        pdf.Space();
        pdf.Line("2. Công nợ hiện tại");
        pdf.Line($"Tổng dư nợ gốc hiện tại: {Money(report.TongDuNoGocHienTai)}");
        pdf.Line($"Nhóm nợ hiện tại: {report.NhomNoCaoNhat}");
        pdf.Line($"Trạng thái khoản vay hiện tại: {(report.SoKhoanVayDangHoatDong > 0 ? "Đang có dư nợ" : "Không có dư nợ")}");
        pdf.Line($"Tỷ lệ nợ / thu nhập: {Percent(report.TyLeNoThuNhap)}");
        pdf.Space();
        pdf.Line("3. Đánh giá tín dụng");
        pdf.Line($"Điểm tín dụng: {Text(report.DiemTinDung?.ToString())}");
        pdf.Line($"Xếp hạng rủi ro: {Text(report.XepHangRuiRo)}");
        pdf.Line($"Số lần trả trễ: {report.SoLanTraTre}");
        pdf.Line($"Nguồn cập nhật: {Text(report.NguonCapNhatTinDung)}");
        pdf.Line($"Ngày cập nhật: {FormatDateTime(report.NgayCapNhatTinDung)}");
        pdf.Space();
        pdf.Line("Khách hàng có khả năng tài chính:");
        pdf.Line($"{Check(report.CoKhaNangTaiChinh)} Có     {Check(!report.CoKhaNangTaiChinh)} Không");
        pdf.Line("Nhận xét tình hình tài chính:");
        pdf.Line(Dots());
        pdf.Space();

        pdf.Section("IV. HẠN MỨC TÍN DỤNG");
        pdf.Line($"Hạn mức đề nghị cấp theo đơn vay: {Money(model.Loan.SoTienYeuCau)}");
        pdf.Line($"Kỳ hạn đề nghị: {model.KyHanDeNghi} tháng");
        pdf.Line($"Lãi suất đề nghị: {(model.LaiSuatDeNghi.HasValue ? $"{model.LaiSuatDeNghi:0.##}%/năm" : Blank())}");
        pdf.Line($"Mục đích vay: {model.Loan.MucDichVay}");
        pdf.Space();
        pdf.Line($"Hạn mức tối đa đã cấp trước đó: {Money(report.HanMucToiDa)}");
        pdf.Line($"Hạn mức đã sử dụng trước đó: {Money(report.HanMucDaSuDung)}");
        pdf.Line($"Hạn mức còn lại trước khi xét đơn: {Money(report.HanMucConLai)}");
        pdf.Line($"Hạn mức gợi ý theo tài sản bảo đảm: {Money(model.HanMucGoiY)}");
        pdf.Line($"Số tiền có thể vay thêm: {Money(AvailableLoanLimit(report, model))}");
        pdf.Line($"Ngày cập nhật hạn mức: {FormatDate(report.NgayCapNhatHanMuc)}");
        pdf.Space();
        pdf.Line("Số tiền đề nghị có phù hợp hạn mức còn lại / tài sản bảo đảm:");
        var availableLoanLimit = AvailableLoanLimit(report, model);
        bool? amountFitsLimit = availableLoanLimit.HasValue
            ? availableLoanLimit.Value >= model.Loan.SoTienYeuCau
            : null;
        pdf.Line($"{Check(amountFitsLimit)} Có     {Check(!amountFitsLimit)} Không");
        pdf.Line("Nhận xét hạn mức:");
        pdf.Line(Dots());
        pdf.Space();

        pdf.Section("V. TÀI SẢN BẢO ĐẢM");
        pdf.Line("1. Danh sách tài sản khách hàng khai báo");
        if (model.TaiSanDamBao.Count == 0)
        {
            pdf.Line("Chưa có tài sản bảo đảm.");
        }
        else
        {
            pdf.AssetTable(model.TaiSanDamBao);
        }

        pdf.Line("2. Đánh giá tài sản bảo đảm");
        pdf.Line($"Tổng giá trị tài sản định giá: {Money(model.TongGiaTriDamBao)}");
        pdf.Line($"Tổng giá trị có thể bảo đảm theo LTV: {Money(model.HanMucGoiY)}");
        pdf.Line("Tài sản còn thuộc sở hữu khách hàng:");
        var allAssetsOwned = model.TaiSanDamBao.Count > 0 && model.TaiSanDamBao.All(x => x.TrangThaiSoHuu == "Đang sở hữu");
        pdf.Line($"{Check(allAssetsOwned)} Có     {Check(!allAssetsOwned)} Không");
        pdf.Line("Tài sản đang bị thế chấp ở khoản vay khác:");
        pdf.Line($"{Check()} Có     {Check()} Không");
        pdf.Line("Điều kiện bảo đảm nợ vay đạt yêu cầu:");
        pdf.Line($"{Check(report.DuDieuKienDamBao)} Có     {Check(!report.DuDieuKienDamBao)} Không");
        pdf.Line("Nhận xét tài sản bảo đảm:");
        pdf.Line(Dots());
        pdf.Space();

        pdf.Section("VI. ĐÁNH GIÁ RỦI RO");
        pdf.Line($"Nhóm nợ cao nhất của khách hàng: {report.NhomNoCaoNhat}");
        pdf.Line("Có khoản vay quá hạn:");
        pdf.Line($"{Check(report.CoNoQuaHan)} Có     {Check(!report.CoNoQuaHan)} Không");
        pdf.Line("Có nợ xấu / nợ nhóm 3 trở lên:");
        pdf.Line($"{Check(report.CoNoXau)} Có     {Check(!report.CoNoXau)} Không");
        pdf.Line($"Mức độ rủi ro theo điểm tín dụng: {Text(report.XepHangRuiRo)}");
        pdf.Line("Nhận xét rủi ro:");
        pdf.Line(Dots());
        pdf.Space();

        pdf.Section("VII. Ý KIẾN CÁN BỘ TÍN DỤNG");
        pdf.Line("Sau khi thẩm định hồ sơ khách hàng, tình hình tài chính, lịch sử tín dụng và tài sản bảo đảm, cán bộ tín dụng đề xuất:");
        pdf.Line($"{Check(!report.DeXuatChoVay)} Không đồng ý cho vay");
        pdf.Line("Lý do:");
        pdf.Line(Dots());
        pdf.Space();
        pdf.Line($"{Check(report.DeXuatChoVay)} Đồng ý cho vay");
        pdf.Line($"Số tiền đề nghị cho vay: {Money(model.Loan.SoTienYeuCau)}");
        pdf.Line($"Lãi suất đề nghị: {(model.LaiSuatDeNghi.HasValue ? $"{model.LaiSuatDeNghi:0.##}%/năm" : Blank())}");
        pdf.Line($"Thời hạn vay: {model.KyHanDeNghi} tháng");
        pdf.Line("Phương thức trả nợ: ......................................................");
        pdf.Line($"Mục đích vay: {model.Loan.MucDichVay}");
        pdf.Line("Kiến nghị khác: ........................................................................................");
        pdf.Signature("Cán bộ tín dụng");

        pdf.Section("VIII. Ý KIẾN CẤP PHÊ DUYỆT");
        pdf.Line($"Cấp phê duyệt: {Text(approval?.CapPheDuyet.ToString())}");
        pdf.Line($"Nhân viên phê duyệt: {Text(approval?.MaNv)}");
        pdf.Line($"Trạng thái phê duyệt: {Text(approval?.TrangThai)}");
        pdf.Line($"Ngày xử lý: {FormatDateTime(approval?.NgayXuLy)}");
        pdf.Line($"{Check(approval?.TrangThai == "Từ chối")} Không đồng ý cho vay");
        pdf.Line("Lý do:");
        pdf.Line(Dots());
        pdf.Line($"{Check(approval?.TrangThai == "Đã duyệt")} Đồng ý cho vay");
        pdf.Line($"Ghi chú phê duyệt: {Text(approval?.GhiChu)}");
        pdf.Signature("Người phê duyệt");

        pdf.Section("IX. QUYẾT ĐỊNH CHO VAY");
        pdf.Line($"{Check(model.Loan.TrangThaiDon == "Từ chối")} Không chấp thuận cho vay");
        pdf.Line("Lý do:");
        pdf.Line(Dots());
        pdf.Line($"{Check(model.Loan.TrangThaiDon == "Đã duyệt")} Chấp thuận cho vay");
        pdf.Line($"Số tiền cho vay: {Money(model.Loan.SoTienYeuCau)}");
        pdf.Line($"Số tiền có thể vay thêm: {Money(AvailableLoanLimit(report, model))}");
        pdf.Line($"Lãi suất: {(model.LaiSuatDeNghi.HasValue ? $"{model.LaiSuatDeNghi:0.##}%/năm" : Blank())}");
        pdf.Line($"Thời hạn vay: {model.KyHanDeNghi} tháng");
        pdf.Line("Ngày giải ngân dự kiến: ................................................");
        pdf.Line("Ngày đáo hạn: ................................................");
        pdf.Line("Phương thức trả nợ: ................................................");
        pdf.Line("Kiến nghị khác:");
        pdf.Line(Dots());
        pdf.Signature("Giám đốc / Người có thẩm quyền");

        document.Close();
        return ms.ToArray();
    }

    private static string FormatDate(DateOnly? value) => value.HasValue ? value.Value.ToString("dd/MM/yyyy") : Blank();

    private static string FormatDateTime(DateTime? value) => value.HasValue ? value.Value.ToString("dd/MM/yyyy") : Blank();

    private static string CustomerDateLine(LoanAppraisalReportViewModel report)
    {
        var isBusiness = string.Equals(report.LoaiKhachHang, "Doanh nghiệp", StringComparison.OrdinalIgnoreCase);
        return isBusiness
            ? $"Ngày thành lập: {FormatDate(report.NgayThanhLap)}"
            : $"Ngày sinh: {report.NgaySinh:dd/MM/yyyy}";
    }

    private static decimal? AvailableLoanLimit(LoanAppraisalReportViewModel report, LoanDetailViewModel model)
    {
        if (report.HanMucConLai.HasValue && model.HanMucGoiY > 0)
        {
            return Math.Min(report.HanMucConLai.Value, model.HanMucGoiY);
        }

        return report.HanMucConLai ?? (model.HanMucGoiY > 0 ? model.HanMucGoiY : null);
    }

    private static string Check() => "[ ]";

    private static string Check(bool value) => value ? "[x]" : "[ ]";

    private static string Check(bool? value) => value.HasValue ? Check(value.Value) : Check();

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? Blank() : value;

    private static string Blank() => "................................................";

    private static string Dots() => "........................................................................................................................";

    private static string Money(decimal? value) => value.HasValue ? Money(value.Value) : Blank();

    private static string Money(decimal value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " VND";

    private static string Percent(double? value) => value.HasValue ? $"{value.Value * 100d:0.0}%" : Blank();

    private sealed class PdfBuilder
    {
        private readonly Document _document;
        private readonly Font _titleFont;
        private readonly Font _sectionFont;
        private readonly Font _normalFont;
        private readonly Font _smallFont;
        private readonly Font _boldFont;
        private readonly Font _italicFont;

        public PdfBuilder(Document document)
        {
            _document = document;
            var baseFont = BaseFont.CreateFont(GetTimesNewRomanPath(), BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            _titleFont = new Font(baseFont, 14f, Font.BOLD);
            _sectionFont = new Font(baseFont, 11.5f, Font.BOLD);
            _normalFont = new Font(baseFont, 10f, Font.NORMAL);
            _smallFont = new Font(baseFont, 8.5f, Font.NORMAL);
            _boldFont = new Font(baseFont, 10.5f, Font.BOLD);
            _italicFont = new Font(baseFont, 10.5f, Font.ITALIC);
        }

        public void Header()
        {
            var table = new PdfPTable(new[] { 1f, 1.55f })
            {
                WidthPercentage = 100f,
                SpacingAfter = 12f
            };

            table.AddCell(BorderlessCell("NGÂN HÀNG ..............\nChi nhánh: ........................", _boldFont, Element.ALIGN_LEFT));
            table.AddCell(BorderlessCell(
                "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc\n------------------------------\n\nNgày.....tháng.......năm 20.....",
                _boldFont,
                Element.ALIGN_CENTER,
                _italicFont));

            _document.Add(table);
        }

        public void Title(string text)
        {
            var paragraph = Paragraph(text, _titleFont, 17f);
            paragraph.Alignment = Element.ALIGN_CENTER;
            paragraph.SpacingAfter = 6f;
            _document.Add(paragraph);
        }

        public void Section(string text)
        {
            var paragraph = Paragraph(text, _sectionFont, 14f);
            paragraph.SpacingBefore = 6f;
            paragraph.SpacingAfter = 2f;
            _document.Add(paragraph);
        }

        public void Line(string text) => _document.Add(Paragraph(text, _normalFont, 12f));

        public void Space() => _document.Add(new Paragraph(" ", _normalFont) { Leading = 6f });

        public void Signature(string title)
        {
            var table = new PdfPTable(new[] { 1.2f, 1f })
            {
                WidthPercentage = 100f,
                SpacingBefore = 10f,
                SpacingAfter = 10f,
                KeepTogether = true
            };

            table.AddCell(BorderlessCell(string.Empty, _normalFont, Element.ALIGN_LEFT));
            var cell = BorderlessCell($"{title}\n(Ký, ghi rõ họ tên)\n\n\n\n\n", _normalFont, Element.ALIGN_CENTER);
            cell.MinimumHeight = 82f;
            table.AddCell(cell);
            _document.Add(table);
        }

        public void AssetTable(IReadOnlyCollection<LoanCollateralViewModel> assets)
        {
            var table = new PdfPTable(new[] { 1.1f, 2.2f, 1.8f, 1.8f, 1f, 1.7f, 1.7f })
            {
                WidthPercentage = 100f,
                SpacingBefore = 4f,
                SpacingAfter = 8f
            };

            foreach (var header in new[] { "Mã TS", "Loại tài sản", "Khai báo", "Định giá", "LTV", "Trạng thái", "Sở hữu" })
            {
                table.AddCell(Cell(header, _smallFont, true));
            }

            foreach (var asset in assets)
            {
                table.AddCell(Cell(asset.MaTaiSanKh, _smallFont));
                table.AddCell(Cell($"{asset.LoaiTaiSan}\nGiấy tờ: {Text(asset.GiayToPhapLy)}\nMô tả: {Text(asset.MoTa)}", _smallFont));
                table.AddCell(Cell(Money(asset.GiaTriKhaiBao), _smallFont));
                table.AddCell(Cell(Money(asset.GiaTriDinhGia), _smallFont));
                table.AddCell(Cell($"{asset.TyLeLtv * 100:0.#}%", _smallFont));
                table.AddCell(Cell(asset.TrangThai, _smallFont));
                table.AddCell(Cell(asset.TrangThaiSoHuu, _smallFont));
            }

            _document.Add(table);
        }

        private static Paragraph Paragraph(string text, Font font, float leading)
        {
            return new Paragraph(text ?? string.Empty, font)
            {
                Leading = leading,
                SpacingAfter = 0f
            };
        }

        private static PdfPCell Cell(string text, Font font, bool header = false)
        {
            var cell = new PdfPCell(new Phrase(text ?? string.Empty, font))
            {
                Padding = 4f,
                BorderWidth = .5f
            };

            if (header)
            {
                cell.BackgroundColor = new BaseColor(235, 235, 235);
                cell.HorizontalAlignment = Element.ALIGN_CENTER;
            }

            return cell;
        }

        private static PdfPCell BorderlessCell(string text, Font font, int alignment, Font? lastLineFont = null)
        {
            var paragraph = new Paragraph { Leading = 14f, Alignment = alignment };
            var lines = (text ?? string.Empty).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var lineFont = lastLineFont != null && i == lines.Length - 1 ? lastLineFont : font;
                paragraph.Add(new Chunk(lines[i], lineFont));
                if (i < lines.Length - 1) paragraph.Add(Chunk.NEWLINE);
            }

            return new PdfPCell(paragraph)
            {
                Border = Rectangle.NO_BORDER,
                Padding = 0f,
                HorizontalAlignment = alignment
            };
        }

        private static string GetTimesNewRomanPath()
        {
            var fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var candidates = new[]
            {
                Path.Combine(fontsFolder, "times.ttf"),
                Path.Combine(fontsFolder, "timesbd.ttf"),
                Path.Combine(fontsFolder, "arial.ttf")
            };

            return candidates.FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException("Không tìm thấy font Times New Roman hoặc Arial để xuất PDF tiếng Việt.");
        }
    }
}
