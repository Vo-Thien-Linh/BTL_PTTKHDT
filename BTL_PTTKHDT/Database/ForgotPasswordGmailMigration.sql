/*
    Them cac cot can thiet cho chuc nang quen mat khau qua Gmail.

    Chay script nay tren database QLTDNH hien tai truoc khi dung chuc nang moi.
*/

IF COL_LENGTH('dbo.NhanVien', 'Email') IS NULL
BEGIN
    ALTER TABLE dbo.NhanVien
    ADD Email VARCHAR(150) NULL;
END;
GO

IF COL_LENGTH('dbo.TaiKhoanNhanVien', 'ResetPasswordCodeHash') IS NULL
BEGIN
    ALTER TABLE dbo.TaiKhoanNhanVien
    ADD ResetPasswordCodeHash NVARCHAR(256) NULL;
END;
GO

IF COL_LENGTH('dbo.TaiKhoanNhanVien', 'ResetPasswordExpiresAt') IS NULL
BEGIN
    ALTER TABLE dbo.TaiKhoanNhanVien
    ADD ResetPasswordExpiresAt DATETIME NULL;
END;
GO
