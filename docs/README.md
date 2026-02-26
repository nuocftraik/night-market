# ECO.WebApi Documentation Hub

> 📚 **Central Hub** cho tất cả documentation của ECO.WebApi solution.

---

## 📖 Tài liệu chính (Main Docs)

### 🚀 Quick Start

| Document | Purpose | For Who |
|----------|---------|---------|
| **[SETUP_GUIDE.md](SETUP_GUIDE.md)** | Setup và chạy project có sẵn | Developers joining project |
| **[BUILD_INDEX.md](BUILD_INDEX.md)** | Xây dựng solution từ đầu | Architects, Tech Leads |
| **[MODULE_DOCUMENTATION_TEMPLATE.md](MODULE_DOCUMENTATION_TEMPLATE.md)** | Template viết docs cho modules | Documentation writers |

---

## 🏗️ Build Documentation (Xây dựng từ đầu)

### **Phase 1: Foundation Setup** (Nền tảng)

| Step | Document | Topics | Time Estimate |
|------|----------|--------|---------------|
| 1 | [BUILD_01_Solution_Setup](BUILD_01_Solution_Setup.md) | Solution, Projects, Build Config | 30 mins |
| 2 | [BUILD_02_Shared_Layer](BUILD_02_Shared_Layer.md) | Authorization Constants | 20 mins |
| 3 | [BUILD_03_Domain_Layer](BUILD_03_Domain_Layer.md) | Identity Entities | 45 mins |
| 4 | [BUILD_04_Application_Layer](BUILD_04_Application_Layer.md) | MediatR, FluentValidation | 60 mins |
| 5 | [BUILD_05_Infrastructure_Layer](BUILD_05_Infrastructure_Layer.md) | DbContext, Modular Startup | 90 mins |
| 6 | [BUILD_06_Host_Layer](BUILD_06_Host_Layer.md) | Program.cs, Controllers | 45 mins |

**Total Time: ~5 hours**

---

### **Phase 2: Core Domain & Patterns**

| Step | Document | Topics | Time Estimate |
|------|----------|--------|---------------|
| 7 | [BUILD_09_Domain_Base_Entities](BUILD_09_Domain_Base_Entities.md) | Base Entities, Domain Events | 60 mins |
| 8 | [BUILD_10_Repository_Pattern](BUILD_10_Repository_Pattern.md) | Repository, Specifications | 90 mins |

**Total Time: ~2.5 hours**

---

### **Phase 3: Database & Initialization**

| Step | Document | Topics | Time Estimate |
|------|----------|--------|---------------|
| 9 | [BUILD_07_Database_Initialization](BUILD_07_Database_Initialization.md) | Migrations, Seeding | 120 mins |

**Total Time: ~2 hours**

---

### **Phase 4: Service Layer**

| Step | Document | Topics | Time Estimate |
|------|----------|--------|---------------|
| 10 | [BUILD_08_Service_Registration](BUILD_08_Service_Registration.md) | Auto Service Registration | 30 mins |
| 11 | [BUILD_11_Common_Services](BUILD_11_Common_Services.md) | CurrentUser, Exceptions, Validation | 90 mins |
| 12 | [BUILD_12_Infrastructure_Services](BUILD_12_Infrastructure_Services.md) | Caching, Email, BackgroundJobs | 120 mins |
| 13 | [BUILD_13_Application_Services](BUILD_13_Application_Services.md) | Token, User, Role Services | 90 mins |

**Total Time: ~5.5 hours**

---

## 📚 Learning Path (Lộ trình học)

### **Beginner Path** (Người mới bắt đầu)
```
1. Đọc SETUP_GUIDE.md để Setup project và chạy
2. Explore code trong solution
3. Đọc BUILD_INDEX.md để hiểu overview
4. Đọc BUILD_01 → BUILD_06 để hiểu foundation
```
**Time: 2-3 days**

### **Intermediate Path** (Developer có kinh nghiệm)
```
1. Đọc BUILD_INDEX.md → Overview toàn bộ
2. Đọc Phase 1-4 documents → Hiểu chi tiết
3. Tự implement một feature mới
```
**Time: 1 week**

### **Advanced Path** (Architect/Tech Lead)
```
1. Đọc toàn bộ BUILD docs → Deep understanding
2. Review architecture decisions
3. Contribute improvements
```
**Time: 2 weeks**

---

## 📖 Documentation by Topic

### **Architecture**
- [BUILD_INDEX.md](BUILD_INDEX.md) - Clean Architecture Overview
- [BUILD_05_Infrastructure_Layer.md](BUILD_05_Infrastructure_Layer.md) - Modular Startup Pattern
- [BUILD_09_Domain_Base_Entities.md](BUILD_09_Domain_Base_Entities.md) - Domain-Driven Design

### **Database**
- [BUILD_07_Database_Initialization.md](BUILD_07_Database_Initialization.md) - EF Core, Migrations, Seeding
- [BUILD_10_Repository_Pattern.md](BUILD_10_Repository_Pattern.md) - Repository & Specification

### **Authentication & Authorization**
- [BUILD_02_Shared_Layer.md](BUILD_02_Shared_Layer.md) - Authorization Constants
- [BUILD_13_Application_Services.md](BUILD_13_Application_Services.md) - TokenService, UserService

### **Infrastructure Services**
- [BUILD_12_Infrastructure_Services.md](BUILD_12_Infrastructure_Services.md) - Caching, Email, BackgroundJobs
- [BUILD_11_Common_Services.md](BUILD_11_Common_Services.md) - Common Utilities

### **API Development**
- [BUILD_06_Host_Layer.md](BUILD_06_Host_Layer.md) - Controllers, Swagger
- [BUILD_04_Application_Layer.md](BUILD_04_Application_Layer.md) - DTOs, Validators

---

## 🔧 Quick Reference

### **Common Commands**

**Build & Run**
```bash
dotnet restore
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```

**Database Migrations**
```bash
cd src/Host/Host/
dotnet ef migrations add MigrationName --project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj
dotnet ef database update --project ../../Migrators/Migrators.MSSQL/Migrators.MSSQL.csproj
```

**Testing**
```bash
dotnet test
```

---

### **Important Files**

**Configuration Files** (`src/Host/Host/Configurations/`)
- `database.json` - Database connection
- `security.json` - JWT settings
- `cache.json` - Redis configuration
- `mail.json` - SMTP settings
- `hangfire.json` - Background jobs

**Entry Points**
- `src/Host/Host/Program.cs` - Application startup
- `src/Infrastructure/Infrastructure/Startup.cs` - Infrastructure registration
- `src/Core/Application/Startup.cs` - Application registration

---

### **Default Credentials**

**Admin User**
- Email: `admin@root.com`
- Password: `123Pa$$word!`

**Hangfire Dashboard**
- URL: `https://localhost:7001/hangfire`
- Username: `admin`
- Password: `SecurePwd1!`

---

## 👨‍💻 Development Workflow

### **Adding New Feature**

```
1. Create Domain Entity (Domain Layer)
   → BUILD_03_Domain_Layer.md

2. Create DTOs & Interfaces (Application Layer)
   → BUILD_04_Application_Layer.md

3. Implement Service (Infrastructure Layer)
   → BUILD_05_Infrastructure_Layer.md

4. Create Controller (Host Layer)
   → BUILD_06_Host_Layer.md

5. Create Migration
   → BUILD_07_Database_Initialization.md

6. Test API via Swagger
```

### **Debugging Issues**

**Build Errors**
→ Check [SETUP_GUIDE.md#Troubleshooting](SETUP_GUIDE.md#9-troubleshooting)

**Database Errors**
→ Check [BUILD_07_Database_Initialization.md](BUILD_07_Database_Initialization.md)

**Authentication Errors**
→ Check [BUILD_13_Application_Services.md](BUILD_13_Application_Services.md)

---

## 📊 Documentation Stats

| Category | Documents | Status |
|----------|-----------|--------|
| Setup Guides | 1 | ✅ Complete |
| Build Guides | 13 | ✅ Complete |
| Templates | 1 | ✅ Complete |
| **Total** | **15** | **✅ Complete** |

---

## ✨ Best Practices

### **When Reading Docs**
1. ✅ Start with index/overview
2. ✅ Follow prerequisites order
3. ✅ Try examples hands-on
4. ✅ Take notes of key concepts

### **When Writing Docs**
1. ✅ Use [MODULE_DOCUMENTATION_TEMPLATE.md](MODULE_DOCUMENTATION_TEMPLATE.md)
2. ✅ Include code examples
3. ✅ Add troubleshooting sections
4. ✅ Cross-reference related docs

### **When Contributing**
1. ✅ Update relevant docs với code changes
2. ✅ Add comments trong code
3. ✅ Test all examples trước khi commit
4. ✅ Review formatting và links

---

## 🌐 External Resources

### **Official Documentation**
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/)
- [EF Core 8 Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/)

### **Libraries**
- [MediatR](https://github.com/jbogard/MediatR)
- [FluentValidation](https://docs.fluentvalidation.net/)
- [Mapster](https://github.com/MapsterMapper/Mapster)
- [Hangfire](https://docs.hangfire.io/)
- [Serilog](https://serilog.net/)

### **Patterns & Architecture**
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Repository Pattern](https://martinfowler.com/eaaCatalog/repository.html)
- [Specification Pattern](https://deviq.com/design-patterns/specification-pattern)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)

---

## 💬 Support

### **Need Help?**
- 👉 Check [SETUP_GUIDE.md#Troubleshooting](SETUP_GUIDE.md#9-troubleshooting)
- 👉 [Open GitHub Issue](https://github.com/vuongnv1206/eco/issues)
- 👉 Contact: support@eco.com

### **Want to Contribute?**
- 👉 Follow [MODULE_DOCUMENTATION_TEMPLATE.md](MODULE_DOCUMENTATION_TEMPLATE.md)
- 👉 Submit Pull Request với updated docs
- ✅ Ensure all examples tested

---

## 📝 Documentation Updates

| Date | Update | Author |
|------|--------|--------|
| 2024-01 | Initial documentation structure | Team |
| 2024-01 | Enhanced BUILD_INDEX and SETUP_GUIDE | Team |
| 2024-01 | Added MODULE_DOCUMENTATION_TEMPLATE | Team |

---

## ✅ Quick Checklist

### **For New Developers**
- [ ] Read SETUP_GUIDE.md
- [ ] Setup local environment
- [ ] Run application successfully
- [ ] Login với admin account
- [ ] Test API via Swagger
- [ ] Explore codebase
- [ ] Read BUILD_INDEX.md overview

### **For Understanding Architecture**
- [ ] Read BUILD_INDEX.md completely
- [ ] Understand Clean Architecture layers
- [ ] Read Phase 1 documents (Foundation)
- [ ] Read Phase 2 documents (Patterns)
- [ ] Read Phase 3 documents (Database)
- [ ] Read Phase 4 documents (Services)

### **For Contributing**
- [ ] Understand architecture
- [ ] Follow naming conventions
- [ ] Write unit tests
- [ ] Update documentation
- [ ] Test locally
- [ ] Submit PR với clear description

---

**👉 Start here:**
- 👨‍💻 Developer? → [SETUP_GUIDE.md](SETUP_GUIDE.md)
- 🏗️ Architect? → [BUILD_INDEX.md](BUILD_INDEX.md)
- ✍️ Writer? → [MODULE_DOCUMENTATION_TEMPLATE.md](MODULE_DOCUMENTATION_TEMPLATE.md)

---

*Last Updated: 2024 | Version: 1.0 | Maintained by ECO Team*
