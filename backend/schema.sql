IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Permissions] bigint NOT NULL,
        [GlobalScope] bit NOT NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [FullName] nvarchar(500) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Status] int NOT NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [ApprovedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Audit] (
        [Id] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [Action] nvarchar(500) NOT NULL,
        [EntityType] nvarchar(500) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [Detail] nvarchar(4000) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Audit] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Idempotency] (
        [Id] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [Key] nvarchar(100) NOT NULL,
        [PayloadHash] nvarchar(500) NOT NULL,
        [AttendanceRecordId] uniqueidentifier NOT NULL,
        [ResponseJson] nvarchar(4000) NOT NULL,
        CONSTRAINT [PK_Idempotency] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Students] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [SaintName] nvarchar(500) NULL,
        [FullName] nvarchar(500) NOT NULL,
        [DateOfBirth] date NOT NULL,
        [SearchName] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_Students] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Years] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Years] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [AuthSessions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [Revoked] bit NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AuthSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuthSessions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Catechists] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(500) NOT NULL,
        [SaintName] nvarchar(500) NULL,
        [UserId] uniqueidentifier NULL,
        CONSTRAINT [PK_Catechists] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Catechists_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Credentials] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [IssuedAtUtc] datetime2 NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Credentials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Credentials_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Classes] (
        [Id] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_Classes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Classes_Years_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [Years] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [AuthSessionId] uniqueidentifier NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [Used] bit NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_AuthSessions_AuthSessionId] FOREIGN KEY ([AuthSessionId]) REFERENCES [AuthSessions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Assignments] (
        [Id] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NOT NULL,
        [CatechistId] uniqueidentifier NOT NULL,
        [From] date NOT NULL,
        [Until] date NULL,
        CONSTRAINT [PK_Assignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Assignments_Catechists_CatechistId] FOREIGN KEY ([CatechistId]) REFERENCES [Catechists] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Assignments_Classes_ClassRoomId] FOREIGN KEY ([ClassRoomId]) REFERENCES [Classes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Enrollments] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NOT NULL,
        [From] date NOT NULL,
        [Until] date NULL,
        CONSTRAINT [PK_Enrollments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Enrollments_Classes_ClassRoomId] FOREIGN KEY ([ClassRoomId]) REFERENCES [Classes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Enrollments_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Exceptions] (
        [Id] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Cancelled] bit NOT NULL,
        [OpensAt] time NOT NULL,
        [ClosesAt] time NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_Exceptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Exceptions_Classes_ClassRoomId] FOREIGN KEY ([ClassRoomId]) REFERENCES [Classes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Schedules] (
        [Id] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NOT NULL,
        [DaysMask] int NOT NULL,
        [From] date NOT NULL,
        [Until] date NOT NULL,
        [OpensAt] time NOT NULL,
        [ClosesAt] time NOT NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Schedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Schedules_Classes_ClassRoomId] FOREIGN KEY ([ClassRoomId]) REFERENCES [Classes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Sessions] (
        [Id] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [OpensAtUtc] datetime2 NOT NULL,
        [ClosesAtUtc] datetime2 NOT NULL,
        [Cancelled] bit NOT NULL,
        [ScheduleVersion] int NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Sessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sessions_Classes_ClassRoomId] FOREIGN KEY ([ClassRoomId]) REFERENCES [Classes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Rosters] (
        [Id] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [EnrollmentId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Rosters] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Rosters_SessionId_StudentId] UNIQUE ([SessionId], [StudentId]),
        CONSTRAINT [FK_Rosters_Enrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [Enrollments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Rosters_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Rosters_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE TABLE [Attendance] (
        [Id] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [Present] bit NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        [Method] nvarchar(500) NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Attendance] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Attendance_Rosters_SessionId_StudentId] FOREIGN KEY ([SessionId], [StudentId]) REFERENCES [Rosters] ([SessionId], [StudentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AspNetRoles_Name] ON [AspNetRoles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AspNetUserRoles_UserId_RoleId] ON [AspNetUserRoles] ([UserId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AspNetUsers_Email] ON [AspNetUsers] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Assignments_CatechistId] ON [Assignments] ([CatechistId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Assignments_ClassRoomId] ON [Assignments] ([ClassRoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Attendance_SessionId_StudentId] ON [Attendance] ([SessionId], [StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuthSessions_UserId] ON [AuthSessions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Catechists_UserId] ON [Catechists] ([UserId]) WHERE [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Classes_AcademicYearId_Code] ON [Classes] ([AcademicYearId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Credentials_StudentId] ON [Credentials] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Credentials_TokenHash] ON [Credentials] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enrollments_ClassRoomId] ON [Enrollments] ([ClassRoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enrollments_StudentId] ON [Enrollments] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Exceptions_ClassRoomId_Date] ON [Exceptions] ([ClassRoomId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Idempotency_ActorId_Key] ON [Idempotency] ([ActorId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_AuthSessionId] ON [RefreshTokens] ([AuthSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_Hash] ON [RefreshTokens] ([Hash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Rosters_EnrollmentId] ON [Rosters] ([EnrollmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Rosters_SessionId_StudentId] ON [Rosters] ([SessionId], [StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Rosters_StudentId] ON [Rosters] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Schedules_ClassRoomId] ON [Schedules] ([ClassRoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Sessions_ClassRoomId_Date] ON [Sessions] ([ClassRoomId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Students_Code] ON [Students] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Years_Name] ON [Years] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027140406_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027140406_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

