-- -------------------------------------------------
-- 1. KHÁCH HÀNG
-- -------------------------------------------------
CREATE TABLE KhachHang (
    MaKH           VARCHAR(10) PRIMARY KEY,
    HoTen          NVARCHAR(100)   NOT NULL,
    NgaySinh       DATE            NOT NULL,
    CMND_CCCD      VARCHAR(20)     NOT NULL UNIQUE,
    DiaChi         NVARCHAR(255),
    SoDienThoai    VARCHAR(15)     NOT NULL UNIQUE,
    Email          VARCHAR(150),

    LoaiKhachHang  NVARCHAR(20)    NOT NULL
        CHECK (LoaiKhachHang IN (N'Cá nhân', N'Doanh nghiệp')),

    AnhDaiDienUrl  NVARCHAR(500),

    NgayTao        DATETIME        NOT NULL DEFAULT GETDATE(),
    NgayCapNhat    DATETIME        NOT NULL DEFAULT GETDATE(),

    IsActive       BIT             NOT NULL DEFAULT 1,
    MaSoThue VARCHAR(20) NULL,
    TenNguoiDaiDien NVARCHAR(100) NULL,
    ChucVuNguoiDaiDien NVARCHAR(100) NULL,
    NgayThanhLap DATE NULL,
    LinhVucKinhDoanh NVARCHAR(150) NULL,
    DoanhThuBinhQuanThang MONEY NULL,
    LoiNhuanBinhQuanThang MONEY NULL,
    SoLaoDong INT NULL

);
GO


-- -------------------------------------------------
-- 2. NHÂN VIÊN
-- -------------------------------------------------
CREATE TABLE NhanVien (
    MaNV           VARCHAR(10) PRIMARY KEY,
    HoTen          NVARCHAR(100)   NOT NULL,
    SoDienThoai    VARCHAR(15)     NOT NULL,
    DiaChi         NVARCHAR(255),
    Email          VARCHAR(150),
    NgaySinh       DATE            NOT NULL,

    GioiTinh       NVARCHAR(10)
        CHECK (GioiTinh IN (N'Nam', N'Nữ')),

    VaiTro         NVARCHAR(50)    NOT NULL
        CHECK (VaiTro IN (
            N'Giao dịch viên',
            N'Nhân viên tín dụng',
            N'Nhân viên thu nợ',
            N'Kiểm soát viên',
            N'Trưởng phòng',
            N'Quản trị hệ thống'
        )),

    AnhDaiDienUrl  NVARCHAR(500),

    NgayTao        DATETIME        NOT NULL DEFAULT GETDATE(),

    IsActive       BIT             NOT NULL DEFAULT 1
);
GO


-- -------------------------------------------------
-- 3. TÀI KHOẢN ĐĂNG NHẬP NHÂN VIÊN
-- -------------------------------------------------
CREATE TABLE TaiKhoanNhanVien (
    MaTaiKhoan      VARCHAR(10) PRIMARY KEY,

    MaNV            VARCHAR(10) NOT NULL UNIQUE
        FOREIGN KEY REFERENCES NhanVien(MaNV),

    TenDangNhap     NVARCHAR(50) NOT NULL UNIQUE,
    MatKhauHash     NVARCHAR(256) NOT NULL,

    LanDangNhapCuoi DATETIME,

    SoLanSaiMatKhau TINYINT NOT NULL DEFAULT 0,

    BiKhoa          BIT NOT NULL DEFAULT 0,

    NgayTao         DATETIME NOT NULL DEFAULT GETDATE(),
    NgayCapNhat     DATETIME NOT NULL DEFAULT GETDATE(),
    ResetPasswordCodeHash NVARCHAR(256) NULL,
    ResetPasswordExpiresAt DATETIME NULL
);
GO


-- ============================================================
-- PHẦN 3: LUỒNG XIN VAY (LOAN ORIGINATION)
-- ============================================================

-- -------------------------------------------------
-- 4. ĐƠN VAY
-- -------------------------------------------------
CREATE TABLE DonVay (
    MaDon           VARCHAR(10) PRIMARY KEY,

    MaKH            VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES KhachHang(MaKH),

    MaNVSoan        VARCHAR(10) NULL
        FOREIGN KEY REFERENCES NhanVien(MaNV),

    MucDichVay      NVARCHAR(255) NOT NULL,

    SoTienYeuCau    MONEY NOT NULL
        CHECK (SoTienYeuCau > 0),

    KyHanDeNghi     INT NOT NULL
        CHECK (KyHanDeNghi > 0),

    LaiSuatDeNghi   FLOAT NULL
        CHECK (LaiSuatDeNghi > 0),

    NgayNopDon      DATE NOT NULL DEFAULT GETDATE(),

    TrangThaiDon    NVARCHAR(30) NOT NULL DEFAULT N'Đang soạn'
        CHECK (TrangThaiDon IN (
            N'Đang soạn',
            N'Chờ duyệt',
            N'Đã duyệt',
            N'Từ chối',
            N'Đã hủy'
        )),

    GhiChu          NVARCHAR(500),

    NgayTao         DATETIME NOT NULL DEFAULT GETDATE(),
    NgayCapNhat     DATETIME NOT NULL DEFAULT GETDATE()
);
GO


-- -------------------------------------------------
-- 5. QUY TRÌNH PHÊ DUYỆT
-- -------------------------------------------------
CREATE TABLE QuyTrinhPheDuyet (
    MaPheDuyet      VARCHAR(10) PRIMARY KEY,

    MaDon           VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES DonVay(MaDon),

    MaNV            VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES NhanVien(MaNV),

    CapPheDuyet     TINYINT NOT NULL
        CHECK (CapPheDuyet IN (1, 2, 3)),

    TrangThai       NVARCHAR(20) NOT NULL
        CHECK (TrangThai IN (
            N'Chờ duyệt',
            N'Đã duyệt',
            N'Từ chối'
        )),

    NgayXuLy        DATETIME NOT NULL DEFAULT GETDATE(),

    GhiChu          NVARCHAR(500)
);
GO


-- ============================================================
-- PHẦN 4: TÀI SẢN KHÁCH HÀNG & ĐẢM BẢO
-- ============================================================

-- -------------------------------------------------
-- 6. TÀI SẢN KHÁCH HÀNG
-- -------------------------------------------------
CREATE TABLE TaiSanKhachHang (
    MaTaiSanKH      VARCHAR(10) PRIMARY KEY,

    MaKH            VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES KhachHang(MaKH),

    LoaiTaiSan      NVARCHAR(100) NOT NULL,

    GiaTriKhaiBao   MONEY NOT NULL
        CHECK (GiaTriKhaiBao > 0),

    GiaTriDinhGia   MONEY NULL,

    TyLeLTV         FLOAT NOT NULL DEFAULT 0.70
        CHECK (TyLeLTV > 0 AND TyLeLTV <= 1),

    GiayToPhapLy    NVARCHAR(500),
    MoTa            NVARCHAR(500),

    NgayKhaiBao     DATE NOT NULL DEFAULT GETDATE(),

    NgayDinhGia     DATE NULL,

    MaNVDinhGia     VARCHAR(10) NULL
        FOREIGN KEY REFERENCES NhanVien(MaNV),

    -- Trạng thái định giá
    TrangThai       NVARCHAR(20) NOT NULL DEFAULT N'Chưa định giá'
        CHECK (TrangThai IN (
            N'Chưa định giá',
            N'Đã định giá'
        )),

    -- Trạng thái sở hữu
    TrangThaiSoHuu  NVARCHAR(20) NOT NULL DEFAULT N'Đang sở hữu'
        CHECK (TrangThaiSoHuu IN (
            N'Đang sở hữu',
            N'Đã bán',
            N'Không còn sở hữu'
        )),

    NgayBan         DATE NULL,
    GhiChuSoHuu     NVARCHAR(500) NULL
);
GO


-- ============================================================
-- PHẦN 5: KHOẢN VAY & HỢP ĐỒNG
-- ============================================================

-- -------------------------------------------------
-- 7. KHOẢN VAY
-- -------------------------------------------------
CREATE TABLE KhoanVay (
    MaVay           VARCHAR(10) PRIMARY KEY,

    MaDon           VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES DonVay(MaDon),

    MaKH            VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES KhachHang(MaKH),

    SoTienVay       MONEY NOT NULL
        CHECK (SoTienVay > 0),

    LaiSuat         FLOAT NOT NULL
        CHECK (LaiSuat > 0),

    KyHan           INT NOT NULL
        CHECK (KyHan > 0),

    PhuongThucTraNo NVARCHAR(20) NOT NULL DEFAULT N'Gốc lãi đều'
        CHECK (PhuongThucTraNo IN (
            N'Gốc lãi đều',
            N'Gốc đều lãi giảm',
            N'Cuối kỳ'
        )),

    NgayGiaiNgan    DATE NOT NULL,
    NgayDaoHan      DATE NOT NULL,

    DuNoGoc         MONEY NOT NULL DEFAULT 0,

    TrangThai       NVARCHAR(20) NOT NULL DEFAULT N'Đang vay'
        CHECK (TrangThai IN (
           N'Đang vay',
           N'Đã trả hết',
           N'Quá hạn',
           N'Cơ cấu lại',
           N'Xóa nợ'
        )),

    NhomNo          TINYINT NOT NULL DEFAULT 1
        CHECK (NhomNo BETWEEN 1 AND 5),

    NgayCapNhatNhom DATE NOT NULL DEFAULT GETDATE(),

    GhiChu          NVARCHAR(500),

    NgayTao         DATETIME NOT NULL DEFAULT GETDATE()
);
GO


-- -------------------------------------------------
-- 8. TÀI SẢN THẾ CHẤP
-- -------------------------------------------------
CREATE TABLE TaiSanTheChap (
    MaTaiSan        VARCHAR(10) PRIMARY KEY,

    MaVay           VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES KhoanVay(MaVay),

    MaTaiSanKH      VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES TaiSanKhachHang(MaTaiSanKH),

    GiaTriTheChap   MONEY NOT NULL
        CHECK (GiaTriTheChap > 0),

    NgayTheChap     DATE NOT NULL DEFAULT GETDATE(),

    NgayGiaiChap    DATE NULL,

    -- Chỉ quản lý vòng đời thế chấp
    TrangThai       NVARCHAR(20) NOT NULL DEFAULT N'Đang thế chấp'
        CHECK (TrangThai IN (
           N'Đang thế chấp',
           N'Đã giải chấp',
           N'Xử lý'
        )),

    GhiChu          NVARCHAR(500)
);
GO


-- -------------------------------------------------
-- 9. HỢP ĐỒNG TÍN DỤNG
-- -------------------------------------------------
CREATE TABLE HopDongTinDung (
    MaHopDong       VARCHAR(10) PRIMARY KEY,

    MaVay           VARCHAR(10) NOT NULL UNIQUE
        FOREIGN KEY REFERENCES KhoanVay(MaVay),

    MaNV            VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES NhanVien(MaNV),

    NgayKyHopDong   DATE NOT NULL,

    NoiDung         NVARCHAR(MAX),
    DieuKhoan       NVARCHAR(MAX),

    FileUrl         NVARCHAR(500),

    NgayTao         DATETIME NOT NULL DEFAULT GETDATE()
);
GO


-- -------------------------------------------------
-- 10. HẠN MỨC TÍN DỤNG
-- -------------------------------------------------
CREATE TABLE HanMucTinDung (
    MaKH            VARCHAR(10) PRIMARY KEY
        FOREIGN KEY REFERENCES KhachHang(MaKH),

    HanMucToiDa     MONEY NOT NULL
        CHECK (HanMucToiDa > 0),

    HanMucDaSuDung  MONEY NOT NULL DEFAULT 0
        CHECK (HanMucDaSuDung >= 0),

    HanMucConLai    AS (HanMucToiDa - HanMucDaSuDung),

    NgayCapNhat     DATE NOT NULL DEFAULT GETDATE()
);
GO


-- ============================================================
-- PHẦN 6: GIẢI NGÂN & THANH TOÁN
-- ============================================================

-- -------------------------------------------------
-- 11. LỊCH TRẢ NỢ
-- -------------------------------------------------
CREATE TABLE LichTraNo (
    MaLichTraNo      VARCHAR(10) PRIMARY KEY,

    MaVay            VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES KhoanVay(MaVay),

    KyThu            INT NOT NULL
        CHECK (KyThu > 0),

    NgayPhaiTra      DATE NOT NULL,

    SoTienGoc        MONEY NOT NULL
        CHECK (SoTienGoc >= 0),

    SoTienLai        MONEY NOT NULL
        CHECK (SoTienLai >= 0),

    TongPhaiTra      AS (SoTienGoc + SoTienLai),

    SoTienDaThanhToan MONEY NOT NULL DEFAULT 0,

    TrangThai        NVARCHAR(15) NOT NULL DEFAULT N'Chưa trả'
        CHECK (TrangThai IN (
            N'Chưa trả',
N'Đã trả',
N'Trả một phần',
N'Trễ hạn'
        )),

    NgayThanhToanThucTe DATE NULL,

    GhiChu           NVARCHAR(255)
);
GO


-- -------------------------------------------------
-- 12. THANH TOÁN
-- -------------------------------------------------
CREATE TABLE ThanhToan (
    MaThanhToan      VARCHAR(10) PRIMARY KEY,

    MaVay            VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES KhoanVay(MaVay),

    MaLichTraNo      VARCHAR(10) NULL
        FOREIGN KEY REFERENCES LichTraNo(MaLichTraNo),

    MaNV             VARCHAR(10) NULL
        FOREIGN KEY REFERENCES NhanVien(MaNV),

    SoTienThanhToan  MONEY NOT NULL
        CHECK (SoTienThanhToan > 0),

    SoTienGocTra     MONEY NOT NULL DEFAULT 0
        CHECK (SoTienGocTra >= 0),

    SoTienLaiTra     MONEY NOT NULL DEFAULT 0
        CHECK (SoTienLaiTra >= 0),

    SoTienPhatTra    MONEY NOT NULL DEFAULT 0
        CHECK (SoTienPhatTra >= 0),

    NgayThanhToan    DATETIME NOT NULL DEFAULT GETDATE(),

    HinhThuc         NVARCHAR(30) NOT NULL DEFAULT N'Tiền mặt'
        CHECK (HinhThuc IN (
            N'Tiền mặt',
N'Chuyển khoản',
N'Thu nợ tự động'
        )),

    GhiChu           NVARCHAR(255)
);
GO


-- ============================================================
-- PHẦN 7: LỊCH SỬ & ĐÁNH GIÁ TÍN DỤNG
-- ============================================================

-- -------------------------------------------------
-- 13. LỊCH SỬ TÍN DỤNG
-- -------------------------------------------------
CREATE TABLE LichSuTinDung (
    MaLichSu         VARCHAR(10) PRIMARY KEY,

    MaKH             VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES KhachHang(MaKH),

    DiemTinDung      INT NOT NULL
        CHECK (DiemTinDung BETWEEN 0 AND 1000),

    XepHangRuiRo     NVARCHAR(5) NOT NULL
        CHECK (XepHangRuiRo IN (
            N'AAA', N'AA', N'A',
            N'BBB', N'BB', N'B',
            N'CCC', N'CC', N'C', N'D'
        )),

    SoLanTraTre      INT NOT NULL DEFAULT 0,

    ThuNhapHangThang MONEY NULL,

    TyLeNoThuNhap    FLOAT NULL,

    NguonCapNhat     NVARCHAR(50) NOT NULL DEFAULT N'Hệ thống',

    NgayCapNhat      DATETIME NOT NULL DEFAULT GETDATE(),

    GhiChu           NVARCHAR(500)
);
GO


-- ============================================================
-- PHẦN 8: SAO KÊ & BÁO CÁO
-- ============================================================

-- -------------------------------------------------
-- 14. SAO KÊ TÍN DỤNG
-- -------------------------------------------------
CREATE TABLE SaoKeTinDung (
    MaSaoKe          VARCHAR(10) PRIMARY KEY,

    MaVay            VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES KhoanVay(MaVay),

    NgaySaoKe        DATE NOT NULL,

    DuNoDauKy        MONEY NOT NULL DEFAULT 0,
    TongTraGoc       MONEY NOT NULL DEFAULT 0,
    TongTraLai       MONEY NOT NULL DEFAULT 0,
    DuNoCuoiKy       MONEY NOT NULL DEFAULT 0,

    SoTienPhaiTraKy  MONEY NOT NULL DEFAULT 0,

    SoKyTraTre       INT NOT NULL DEFAULT 0,

    ChiTietGiaoDich  NVARCHAR(MAX),

    NgayTao          DATETIME NOT NULL DEFAULT GETDATE()
);
GO


-- -------------------------------------------------
-- 15. LOG BÁO CÁO TÀI SẢN
-- -------------------------------------------------
CREATE TABLE BaoCaoTaiSanLog (
    MaBaoCao         VARCHAR(10) PRIMARY KEY,

    MaVay            VARCHAR(10) NOT NULL
        FOREIGN KEY REFERENCES KhoanVay(MaVay),

    TongGiaTri       MONEY NOT NULL
        CHECK (TongGiaTri >= 0),

    SoLuongTaiSan    INT NOT NULL
        CHECK (SoLuongTaiSan >= 0),

    TyLeLTVTongHop   FLOAT NULL,

    NgayBaoCao       DATETIME NOT NULL DEFAULT GETDATE(),

    GhiChu           NVARCHAR(255)
);
GO


INSERT INTO NhanVien (
    MaNV, HoTen, SoDienThoai, DiaChi, NgaySinh,
    GioiTinh, VaiTro, AnhDaiDienUrl, IsActive
)
VALUES (
    'NV999', N'Quản trị viên', '0123456788', N'Lê Văn Việt',
    '1990-01-01', N'Nam', N'Quản trị hệ thống', NULL, 1
);

INSERT INTO TaiKhoanNhanVien (
    MaTaiKhoan, MaNV, TenDangNhap, MatKhauHash,
    SoLanSaiMatKhau, BiKhoa
)


VALUES (
    'TK999', 'NV999', N'admin',
    '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92',
    0, 0
);
