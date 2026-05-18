/*
    Chuyen cac gia tri nghiep vu dang luu khong dau sang tieng Viet co dau.

    Chay script nay tren database hien tai sau khi backup du lieu.
    Script se:
    1. Go bo CHECK/DEFAULT constraint cu cua cac cot trang thai/phan loai.
    2. Cap nhat du lieu cu sang gia tri co dau.
    3. Tao lai DEFAULT va CHECK constraint theo gia tri co dau.
*/

SET NOCOUNT ON;
GO

DECLARE @sql NVARCHAR(MAX) = N'';

-- Drop CHECK constraints that reference business enum columns.
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(cc.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(cc.parent_object_id))
    + N' DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';' + CHAR(13)
FROM sys.check_constraints cc
WHERE
    (cc.parent_object_id = OBJECT_ID(N'dbo.KhachHang') AND OBJECT_DEFINITION(cc.object_id) LIKE N'%LoaiKhachHang%')
    OR (cc.parent_object_id = OBJECT_ID(N'dbo.NhanVien') AND (OBJECT_DEFINITION(cc.object_id) LIKE N'%GioiTinh%' OR OBJECT_DEFINITION(cc.object_id) LIKE N'%VaiTro%'))
    OR (cc.parent_object_id = OBJECT_ID(N'dbo.DonVay') AND OBJECT_DEFINITION(cc.object_id) LIKE N'%TrangThaiDon%')
    OR (cc.parent_object_id = OBJECT_ID(N'dbo.QuyTrinhPheDuyet') AND OBJECT_DEFINITION(cc.object_id) LIKE N'%TrangThai%')
    OR (cc.parent_object_id = OBJECT_ID(N'dbo.TaiSanKhachHang') AND (OBJECT_DEFINITION(cc.object_id) LIKE N'%TrangThaiSoHuu%' OR OBJECT_DEFINITION(cc.object_id) LIKE N'%TrangThai%'))
    OR (cc.parent_object_id = OBJECT_ID(N'dbo.KhoanVay') AND (OBJECT_DEFINITION(cc.object_id) LIKE N'%TrangThai%' OR OBJECT_DEFINITION(cc.object_id) LIKE N'%PhuongThucTraNo%'))
    OR (cc.parent_object_id = OBJECT_ID(N'dbo.TaiSanTheChap') AND OBJECT_DEFINITION(cc.object_id) LIKE N'%TrangThai%')
    OR (cc.parent_object_id = OBJECT_ID(N'dbo.LichTraNo') AND OBJECT_DEFINITION(cc.object_id) LIKE N'%TrangThai%')
    OR (cc.parent_object_id = OBJECT_ID(N'dbo.ThanhToan') AND OBJECT_DEFINITION(cc.object_id) LIKE N'%HinhThuc%');

EXEC sp_executesql @sql;
GO

DECLARE @sql NVARCHAR(MAX) = N'';

-- Drop DEFAULT constraints for business enum columns.
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(dc.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(dc.parent_object_id))
    + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';' + CHAR(13)
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE
    (dc.parent_object_id = OBJECT_ID(N'dbo.DonVay') AND c.name = N'TrangThaiDon')
    OR (dc.parent_object_id = OBJECT_ID(N'dbo.TaiSanKhachHang') AND c.name IN (N'TrangThai', N'TrangThaiSoHuu'))
    OR (dc.parent_object_id = OBJECT_ID(N'dbo.KhoanVay') AND c.name IN (N'TrangThai', N'PhuongThucTraNo'))
    OR (dc.parent_object_id = OBJECT_ID(N'dbo.TaiSanTheChap') AND c.name = N'TrangThai')
    OR (dc.parent_object_id = OBJECT_ID(N'dbo.LichTraNo') AND c.name = N'TrangThai')
    OR (dc.parent_object_id = OBJECT_ID(N'dbo.ThanhToan') AND c.name = N'HinhThuc')
    OR (dc.parent_object_id = OBJECT_ID(N'dbo.LichSuTinDung') AND c.name = N'NguonCapNhat');

EXEC sp_executesql @sql;
GO

BEGIN TRANSACTION;

UPDATE KhachHang
SET LoaiKhachHang = CASE LoaiKhachHang
    WHEN N'Ca nhan' THEN N'Cá nhân'
    WHEN N'Doanh nghiep' THEN N'Doanh nghiệp'
    ELSE LoaiKhachHang
END;

UPDATE NhanVien
SET
    GioiTinh = CASE GioiTinh WHEN N'Nu' THEN N'Nữ' ELSE GioiTinh END,
    VaiTro = CASE VaiTro
        WHEN N'Giao dich vien' THEN N'Giao dịch viên'
        WHEN N'Nhan vien tin dung' THEN N'Nhân viên tín dụng'
        WHEN N'Nhan vien thu no' THEN N'Nhân viên thu nợ'
        WHEN N'Kiem soat vien' THEN N'Kiểm soát viên'
        WHEN N'Truong phong' THEN N'Trưởng phòng'
        WHEN N'Quan tri he thong' THEN N'Quản trị hệ thống'
        ELSE VaiTro
    END;

UPDATE DonVay
SET TrangThaiDon = CASE TrangThaiDon
    WHEN N'Dang soan' THEN N'Đang soạn'
    WHEN N'Cho duyet' THEN N'Chờ duyệt'
    WHEN N'Da duyet' THEN N'Đã duyệt'
    WHEN N'Tu choi' THEN N'Từ chối'
    WHEN N'Da huy' THEN N'Đã hủy'
    ELSE TrangThaiDon
END;

UPDATE QuyTrinhPheDuyet
SET TrangThai = CASE TrangThai
    WHEN N'Cho duyet' THEN N'Chờ duyệt'
    WHEN N'Da duyet' THEN N'Đã duyệt'
    WHEN N'Tu choi' THEN N'Từ chối'
    ELSE TrangThai
END;

UPDATE TaiSanKhachHang
SET
    TrangThai = CASE TrangThai
        WHEN N'Chua dinh gia' THEN N'Chưa định giá'
        WHEN N'Da dinh gia' THEN N'Đã định giá'
        ELSE TrangThai
    END,
    TrangThaiSoHuu = CASE TrangThaiSoHuu
        WHEN N'Dang so huu' THEN N'Đang sở hữu'
        WHEN N'Da ban' THEN N'Đã bán'
        WHEN N'Khong con so huu' THEN N'Không còn sở hữu'
        ELSE TrangThaiSoHuu
    END;

UPDATE KhoanVay
SET
    PhuongThucTraNo = CASE PhuongThucTraNo
        WHEN N'Goc lai deu' THEN N'Gốc lãi đều'
        WHEN N'Goc deu lai giam' THEN N'Gốc đều lãi giảm'
        WHEN N'Cuoi ky' THEN N'Cuối kỳ'
        ELSE PhuongThucTraNo
    END,
    TrangThai = CASE TrangThai
        WHEN N'Dang vay' THEN N'Đang vay'
        WHEN N'Da tra het' THEN N'Đã trả hết'
        WHEN N'Qua han' THEN N'Quá hạn'
        WHEN N'Co cau lai' THEN N'Cơ cấu lại'
        WHEN N'Xoa no' THEN N'Xóa nợ'
        ELSE TrangThai
    END;

UPDATE TaiSanTheChap
SET TrangThai = CASE TrangThai
    WHEN N'Dang the chap' THEN N'Đang thế chấp'
    WHEN N'Da giai chap' THEN N'Đã giải chấp'
    WHEN N'Xu ly' THEN N'Xử lý'
    ELSE TrangThai
END;

UPDATE LichTraNo
SET TrangThai = CASE TrangThai
    WHEN N'Chua tra' THEN N'Chưa trả'
    WHEN N'Da tra' THEN N'Đã trả'
    WHEN N'Tra mot phan' THEN N'Trả một phần'
    WHEN N'Tre han' THEN N'Trễ hạn'
    ELSE TrangThai
END;

UPDATE ThanhToan
SET HinhThuc = CASE HinhThuc
    WHEN N'Tien mat' THEN N'Tiền mặt'
    WHEN N'Chuyen khoan' THEN N'Chuyển khoản'
    WHEN N'Thu no tu dong' THEN N'Thu nợ tự động'
    ELSE HinhThuc
END;

UPDATE LichSuTinDung
SET NguonCapNhat = CASE NguonCapNhat
    WHEN N'He thong' THEN N'Hệ thống'
    WHEN N'Tao don vay' THEN N'Tạo đơn vay'
    WHEN N'Giai ngan' THEN N'Giải ngân'
    ELSE NguonCapNhat
END;

COMMIT TRANSACTION;
GO

ALTER TABLE KhachHang
ADD CONSTRAINT CK_KhachHang_LoaiKhachHang
CHECK (LoaiKhachHang IN (N'Cá nhân', N'Doanh nghiệp'));
GO

ALTER TABLE NhanVien
ADD CONSTRAINT CK_NhanVien_GioiTinh
CHECK (GioiTinh IN (N'Nam', N'Nữ'));
GO

ALTER TABLE NhanVien
ADD CONSTRAINT CK_NhanVien_VaiTro
CHECK (VaiTro IN (
    N'Giao dịch viên',
    N'Nhân viên tín dụng',
    N'Nhân viên thu nợ',
    N'Kiểm soát viên',
    N'Trưởng phòng',
    N'Quản trị hệ thống'
));
GO

ALTER TABLE DonVay ADD CONSTRAINT DF_DonVay_TrangThaiDon DEFAULT N'Đang soạn' FOR TrangThaiDon;
ALTER TABLE DonVay
ADD CONSTRAINT CK_DonVay_TrangThaiDon
CHECK (TrangThaiDon IN (N'Đang soạn', N'Chờ duyệt', N'Đã duyệt', N'Từ chối', N'Đã hủy'));
GO

ALTER TABLE QuyTrinhPheDuyet
ADD CONSTRAINT CK_QuyTrinhPheDuyet_TrangThai
CHECK (TrangThai IN (N'Chờ duyệt', N'Đã duyệt', N'Từ chối'));
GO

ALTER TABLE TaiSanKhachHang ADD CONSTRAINT DF_TaiSanKhachHang_TrangThai DEFAULT N'Chưa định giá' FOR TrangThai;
ALTER TABLE TaiSanKhachHang ADD CONSTRAINT DF_TaiSanKhachHang_TrangThaiSoHuu DEFAULT N'Đang sở hữu' FOR TrangThaiSoHuu;
ALTER TABLE TaiSanKhachHang
ADD CONSTRAINT CK_TaiSanKhachHang_TrangThai
CHECK (TrangThai IN (N'Chưa định giá', N'Đã định giá'));
ALTER TABLE TaiSanKhachHang
ADD CONSTRAINT CK_TaiSanKhachHang_TrangThaiSoHuu
CHECK (TrangThaiSoHuu IN (N'Đang sở hữu', N'Đã bán', N'Không còn sở hữu'));
GO

ALTER TABLE KhoanVay ADD CONSTRAINT DF_KhoanVay_PhuongThucTraNo DEFAULT N'Gốc lãi đều' FOR PhuongThucTraNo;
ALTER TABLE KhoanVay ADD CONSTRAINT DF_KhoanVay_TrangThai DEFAULT N'Đang vay' FOR TrangThai;
ALTER TABLE KhoanVay
ADD CONSTRAINT CK_KhoanVay_PhuongThucTraNo
CHECK (PhuongThucTraNo IN (N'Gốc lãi đều', N'Gốc đều lãi giảm', N'Cuối kỳ'));
ALTER TABLE KhoanVay
ADD CONSTRAINT CK_KhoanVay_TrangThai
CHECK (TrangThai IN (N'Đang vay', N'Đã trả hết', N'Quá hạn', N'Cơ cấu lại', N'Xóa nợ'));
GO

ALTER TABLE TaiSanTheChap ADD CONSTRAINT DF_TaiSanTheChap_TrangThai DEFAULT N'Đang thế chấp' FOR TrangThai;
ALTER TABLE TaiSanTheChap
ADD CONSTRAINT CK_TaiSanTheChap_TrangThai
CHECK (TrangThai IN (N'Đang thế chấp', N'Đã giải chấp', N'Xử lý'));
GO

ALTER TABLE LichTraNo ADD CONSTRAINT DF_LichTraNo_TrangThai DEFAULT N'Chưa trả' FOR TrangThai;
ALTER TABLE LichTraNo
ADD CONSTRAINT CK_LichTraNo_TrangThai
CHECK (TrangThai IN (N'Chưa trả', N'Đã trả', N'Trả một phần', N'Trễ hạn'));
GO

ALTER TABLE ThanhToan ADD CONSTRAINT DF_ThanhToan_HinhThuc DEFAULT N'Tiền mặt' FOR HinhThuc;
ALTER TABLE ThanhToan
ADD CONSTRAINT CK_ThanhToan_HinhThuc
CHECK (HinhThuc IN (N'Tiền mặt', N'Chuyển khoản', N'Thu nợ tự động'));
GO

ALTER TABLE LichSuTinDung ADD CONSTRAINT DF_LichSuTinDung_NguonCapNhat DEFAULT N'Hệ thống' FOR NguonCapNhat;
GO
