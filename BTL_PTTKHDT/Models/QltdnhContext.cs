using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Models;

public partial class QltdnhContext : DbContext
{
    public QltdnhContext()
    {
    }

    public QltdnhContext(DbContextOptions<QltdnhContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BaoCaoTaiSanLog> BaoCaoTaiSanLogs { get; set; }

    public virtual DbSet<DonVay> DonVays { get; set; }

    public virtual DbSet<HanMucTinDung> HanMucTinDungs { get; set; }

    public virtual DbSet<HopDongTinDung> HopDongTinDungs { get; set; }

    public virtual DbSet<KhachHang> KhachHangs { get; set; }

    public virtual DbSet<KhoanVay> KhoanVays { get; set; }

    public virtual DbSet<LichSuTinDung> LichSuTinDungs { get; set; }

    public virtual DbSet<LichTraNo> LichTraNos { get; set; }

    public virtual DbSet<NhanVien> NhanViens { get; set; }

    public virtual DbSet<QuyTrinhPheDuyet> QuyTrinhPheDuyets { get; set; }

    public virtual DbSet<SaoKeTinDung> SaoKeTinDungs { get; set; }

    public virtual DbSet<TaiKhoanNhanVien> TaiKhoanNhanViens { get; set; }

    public virtual DbSet<TaiSanKhachHang> TaiSanKhachHangs { get; set; }

    public virtual DbSet<TaiSanTheChap> TaiSanTheChaps { get; set; }

    public virtual DbSet<ThanhToan> ThanhToans { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BaoCaoTaiSanLog>(entity =>
        {
            entity.HasKey(e => e.MaBaoCao).HasName("PK__BaoCaoTa__25A9188C1C6A4238");

            entity.ToTable("BaoCaoTaiSanLog");

            entity.Property(e => e.GhiChu).HasMaxLength(255);
            entity.Property(e => e.NgayBaoCao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TongGiaTri).HasColumnType("money");
            entity.Property(e => e.TyLeLtvtongHop).HasColumnName("TyLeLTVTongHop");

            entity.HasOne(d => d.MaVayNavigation).WithMany(p => p.BaoCaoTaiSanLogs)
                .HasForeignKey(d => d.MaVay)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__BaoCaoTai__MaVay__2FCF1A8A");
        });

        modelBuilder.Entity<DonVay>(entity =>
        {
            entity.HasKey(e => e.MaDon).HasName("PK__DonVay__3D89F568C04434C0");

            entity.ToTable("DonVay");

            entity.Property(e => e.GhiChu).HasMaxLength(500);
            entity.Property(e => e.MaKh).HasColumnName("MaKH");
            entity.Property(e => e.MaNvsoan).HasColumnName("MaNVSoan");
            entity.Property(e => e.MucDichVay).HasMaxLength(255);
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NgayNopDon).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoTienYeuCau).HasColumnType("money");
            entity.Property(e => e.TrangThaiDon)
                .HasMaxLength(30)
                .HasDefaultValue("Dang soan");

            entity.HasOne(d => d.MaKhNavigation).WithMany(p => p.DonVays)
                .HasForeignKey(d => d.MaKh)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DonVay__MaKH__4D94879B");

            entity.HasOne(d => d.MaNvsoanNavigation).WithMany(p => p.DonVays)
                .HasForeignKey(d => d.MaNvsoan)
                .HasConstraintName("FK__DonVay__MaNVSoan__4E88ABD4");
        });

        modelBuilder.Entity<HanMucTinDung>(entity =>
        {
            entity.HasKey(e => e.MaKh).HasName("PK__HanMucTi__2725CF1E82E7AFB0");

            entity.ToTable("HanMucTinDung");

            entity.Property(e => e.MaKh)
                .ValueGeneratedNever()
                .HasColumnName("MaKH");
            entity.Property(e => e.HanMucConLai)
                .HasComputedColumnSql("([HanMucToiDa]-[HanMucDaSuDung])", false)
                .HasColumnType("money");
            entity.Property(e => e.HanMucDaSuDung).HasColumnType("money");
            entity.Property(e => e.HanMucToiDa).HasColumnType("money");
            entity.Property(e => e.NgayCapNhat).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MaKhNavigation).WithOne(p => p.HanMucTinDung)
                .HasForeignKey<HanMucTinDung>(d => d.MaKh)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__HanMucTinD__MaKH__01142BA1");
        });

        modelBuilder.Entity<HopDongTinDung>(entity =>
        {
            entity.HasKey(e => e.MaHopDong).HasName("PK__HopDongT__36DD43428F73CE62");

            entity.ToTable("HopDongTinDung");

            entity.HasIndex(e => e.MaVay, "UQ__HopDongT__31CE85B1FA60C732").IsUnique();

            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.MaNv).HasColumnName("MaNV");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.MaNvNavigation).WithMany(p => p.HopDongTinDungs)
                .HasForeignKey(d => d.MaNv)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__HopDongTin__MaNV__7D439ABD");

            entity.HasOne(d => d.MaVayNavigation).WithOne(p => p.HopDongTinDung)
                .HasForeignKey<HopDongTinDung>(d => d.MaVay)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__HopDongTi__MaVay__7C4F7684");
        });

        modelBuilder.Entity<KhachHang>(entity =>
        {
            entity.HasKey(e => e.MaKh).HasName("PK__KhachHan__2725CF1E216BE9C6");

            entity.ToTable("KhachHang");

            entity.HasIndex(e => e.SoDienThoai, "UQ__KhachHan__0389B7BD09FB62B9").IsUnique();

            entity.HasIndex(e => e.CmndCccd, "UQ__KhachHan__B91373138AC39C09").IsUnique();

            entity.Property(e => e.MaKh).HasColumnName("MaKH");
            entity.Property(e => e.AnhDaiDienUrl).HasMaxLength(500);
            entity.Property(e => e.CmndCccd)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("CMND_CCCD");
            entity.Property(e => e.DiaChi).HasMaxLength(255);
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LoaiKhachHang).HasMaxLength(20);
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoDienThoai)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KhoanVay>(entity =>
        {
            entity.HasKey(e => e.MaVay).HasName("PK__KhoanVay__31CE85B05A0AB103");

            entity.ToTable("KhoanVay");

            entity.Property(e => e.DuNoGoc).HasColumnType("money");
            entity.Property(e => e.GhiChu).HasMaxLength(500);
            entity.Property(e => e.MaKh).HasColumnName("MaKH");
            entity.Property(e => e.NgayCapNhatNhom).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NhomNo).HasDefaultValue((byte)1);
            entity.Property(e => e.PhuongThucTraNo)
                .HasMaxLength(20)
                .HasDefaultValue("Goc lai deu");
            entity.Property(e => e.SoTienVay).HasColumnType("money");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("Dang vay");

            entity.HasOne(d => d.MaDonNavigation).WithMany(p => p.KhoanVays)
                .HasForeignKey(d => d.MaDon)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__KhoanVay__MaDon__6C190EBB");

            entity.HasOne(d => d.MaKhNavigation).WithMany(p => p.KhoanVays)
                .HasForeignKey(d => d.MaKh)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__KhoanVay__MaKH__6D0D32F4");
        });

        modelBuilder.Entity<LichSuTinDung>(entity =>
        {
            entity.HasKey(e => e.MaLichSu).HasName("PK__LichSuTi__C443222A970D844C");

            entity.ToTable("LichSuTinDung");

            entity.Property(e => e.GhiChu).HasMaxLength(500);
            entity.Property(e => e.MaKh).HasColumnName("MaKH");
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NguonCapNhat)
                .HasMaxLength(50)
                .HasDefaultValue("He thong");
            entity.Property(e => e.ThuNhapHangThang).HasColumnType("money");
            entity.Property(e => e.XepHangRuiRo).HasMaxLength(5);

            entity.HasOne(d => d.MaKhNavigation).WithMany(p => p.LichSuTinDungs)
                .HasForeignKey(d => d.MaKh)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LichSuTinD__MaKH__1EA48E88");
        });

        modelBuilder.Entity<LichTraNo>(entity =>
        {
            entity.HasKey(e => e.MaLichTraNo).HasName("PK__LichTraN__30B56AAD1B9F56CA");

            entity.ToTable("LichTraNo");

            entity.Property(e => e.GhiChu).HasMaxLength(255);
            entity.Property(e => e.SoTienDaThanhToan).HasColumnType("money");
            entity.Property(e => e.SoTienGoc).HasColumnType("money");
            entity.Property(e => e.SoTienLai).HasColumnType("money");
            entity.Property(e => e.TongPhaiTra)
                .HasComputedColumnSql("([SoTienGoc]+[SoTienLai])", false)
                .HasColumnType("money");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(15)
                .HasDefaultValue("Chua tra");

            entity.HasOne(d => d.MaVayNavigation).WithMany(p => p.LichTraNos)
                .HasForeignKey(d => d.MaVay)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LichTraNo__MaVay__07C12930");
        });

        modelBuilder.Entity<NhanVien>(entity =>
        {
            entity.HasKey(e => e.MaNv).HasName("PK__NhanVien__2725D70A405E36EA");

            entity.ToTable("NhanVien");

            entity.Property(e => e.MaNv).HasColumnName("MaNV");
            entity.Property(e => e.AnhDaiDienUrl).HasMaxLength(500);
            entity.Property(e => e.DiaChi).HasMaxLength(255);
            entity.Property(e => e.GioiTinh).HasMaxLength(10);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoDienThoai)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.VaiTro).HasMaxLength(50);
        });

        modelBuilder.Entity<QuyTrinhPheDuyet>(entity =>
        {
            entity.HasKey(e => e.MaPheDuyet).HasName("PK__QuyTrinh__D14CE0E09495D1DE");

            entity.ToTable("QuyTrinhPheDuyet");

            entity.Property(e => e.GhiChu).HasMaxLength(500);
            entity.Property(e => e.MaNv).HasColumnName("MaNV");
            entity.Property(e => e.NgayXuLy)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TrangThai).HasMaxLength(20);

            entity.HasOne(d => d.MaDonNavigation).WithMany(p => p.QuyTrinhPheDuyets)
                .HasForeignKey(d => d.MaDon)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QuyTrinhP__MaDon__59063A47");

            entity.HasOne(d => d.MaNvNavigation).WithMany(p => p.QuyTrinhPheDuyets)
                .HasForeignKey(d => d.MaNv)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QuyTrinhPh__MaNV__59FA5E80");
        });

        modelBuilder.Entity<SaoKeTinDung>(entity =>
        {
            entity.HasKey(e => e.MaSaoKe).HasName("PK__SaoKeTin__AA2C18D06F478A98");

            entity.ToTable("SaoKeTinDung");

            entity.Property(e => e.DuNoCuoiKy).HasColumnType("money");
            entity.Property(e => e.DuNoDauKy).HasColumnType("money");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoTienPhaiTraKy).HasColumnType("money");
            entity.Property(e => e.TongTraGoc).HasColumnType("money");
            entity.Property(e => e.TongTraLai).HasColumnType("money");

            entity.HasOne(d => d.MaVayNavigation).WithMany(p => p.SaoKeTinDungs)
                .HasForeignKey(d => d.MaVay)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SaoKeTinD__MaVay__2645B050");
        });

        modelBuilder.Entity<TaiKhoanNhanVien>(entity =>
        {
            entity.HasKey(e => e.MaTaiKhoan).HasName("PK__TaiKhoan__AD7C6529F64AA0C6");

            entity.ToTable("TaiKhoanNhanVien");

            entity.HasIndex(e => e.MaNv, "UQ__TaiKhoan__2725D70BBCFBBE83").IsUnique();

            entity.HasIndex(e => e.TenDangNhap, "UQ__TaiKhoan__55F68FC07A8B11BF").IsUnique();

            entity.Property(e => e.LanDangNhapCuoi).HasColumnType("datetime");
            entity.Property(e => e.MaNv).HasColumnName("MaNV");
            entity.Property(e => e.MatKhauHash).HasMaxLength(256);
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TenDangNhap).HasMaxLength(50);

            entity.HasOne(d => d.MaNvNavigation).WithOne(p => p.TaiKhoanNhanVien)
                .HasForeignKey<TaiKhoanNhanVien>(d => d.MaNv)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TaiKhoanNh__MaNV__46E78A0C");
        });

        modelBuilder.Entity<TaiSanKhachHang>(entity =>
        {
            entity.HasKey(e => e.MaTaiSanKh).HasName("PK__TaiSanKh__55C583F50A928C82");

            entity.ToTable("TaiSanKhachHang");

            entity.Property(e => e.MaTaiSanKh).HasColumnName("MaTaiSanKH");
            entity.Property(e => e.GiaTriDinhGia).HasColumnType("money");
            entity.Property(e => e.GiaTriKhaiBao).HasColumnType("money");
            entity.Property(e => e.GiayToPhapLy).HasMaxLength(500);
            entity.Property(e => e.LoaiTaiSan).HasMaxLength(100);
            entity.Property(e => e.MaKh).HasColumnName("MaKH");
            entity.Property(e => e.MaNvdinhGia).HasColumnName("MaNVDinhGia");
            entity.Property(e => e.MoTa).HasMaxLength(500);
            entity.Property(e => e.NgayKhaiBao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("Chua dinh gia");
            entity.Property(e => e.TyLeLtv)
                .HasDefaultValue(0.69999999999999996)
                .HasColumnName("TyLeLTV");

            entity.HasOne(d => d.MaKhNavigation).WithMany(p => p.TaiSanKhachHangs)
                .HasForeignKey(d => d.MaKh)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TaiSanKhac__MaKH__5FB337D6");

            entity.HasOne(d => d.MaNvdinhGiaNavigation).WithMany(p => p.TaiSanKhachHangs)
                .HasForeignKey(d => d.MaNvdinhGia)
                .HasConstraintName("FK__TaiSanKha__MaNVD__6477ECF3");
        });

        modelBuilder.Entity<TaiSanTheChap>(entity =>
        {
            entity.HasKey(e => e.MaTaiSan).HasName("PK__TaiSanTh__8DB7C7BEB83E0566");

            entity.ToTable("TaiSanTheChap");

            entity.Property(e => e.GhiChu).HasMaxLength(500);
            entity.Property(e => e.GiaTriTheChap).HasColumnType("money");
            entity.Property(e => e.MaTaiSanKh).HasColumnName("MaTaiSanKH");
            entity.Property(e => e.NgayTheChap).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("Dang the chap");

            entity.HasOne(d => d.MaTaiSanKhNavigation).WithMany(p => p.TaiSanTheChaps)
                .HasForeignKey(d => d.MaTaiSanKh)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TaiSanThe__MaTai__3D2915A8");

            entity.HasOne(d => d.MaVayNavigation).WithMany(p => p.TaiSanTheChaps)
                .HasForeignKey(d => d.MaVay)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TaiSanThe__MaVay__3C34F16F");
        });

        modelBuilder.Entity<ThanhToan>(entity =>
        {
            entity.HasKey(e => e.MaThanhToan).HasName("PK__ThanhToa__D4B25844244FBB66");

            entity.ToTable("ThanhToan");

            entity.Property(e => e.GhiChu).HasMaxLength(255);
            entity.Property(e => e.HinhThuc)
                .HasMaxLength(30)
                .HasDefaultValue("Tien mat");
            entity.Property(e => e.MaNv).HasColumnName("MaNV");
            entity.Property(e => e.NgayThanhToan)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoTienGocTra).HasColumnType("money");
            entity.Property(e => e.SoTienLaiTra).HasColumnType("money");
            entity.Property(e => e.SoTienPhatTra).HasColumnType("money");
            entity.Property(e => e.SoTienThanhToan).HasColumnType("money");

            entity.HasOne(d => d.MaLichTraNoNavigation).WithMany(p => p.ThanhToans)
                .HasForeignKey(d => d.MaLichTraNo)
                .HasConstraintName("FK__ThanhToan__MaLic__114A936A");

            entity.HasOne(d => d.MaNvNavigation).WithMany(p => p.ThanhToans)
                .HasForeignKey(d => d.MaNv)
                .HasConstraintName("FK__ThanhToan__MaNV__123EB7A3");

            entity.HasOne(d => d.MaVayNavigation).WithMany(p => p.ThanhToans)
                .HasForeignKey(d => d.MaVay)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ThanhToan__MaVay__10566F31");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
