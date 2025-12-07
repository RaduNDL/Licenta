 using Licenta.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Licenta.Areas.Identity.Data
{
    public partial class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<PatientProfile> Patients => Set<PatientProfile>();
        public DbSet<DoctorProfile> Doctors => Set<DoctorProfile>();
        public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<LabResult> LabResults => Set<LabResult>();
        public DbSet<MedicalAttachment> MedicalAttachments => Set<MedicalAttachment>();
        public DbSet<InternalMessage> InternalMessages => Set<InternalMessage>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
        public DbSet<Prediction> Predictions => Set<Prediction>();
        public DbSet<MlIntakeForm> MlIntakeForms => Set<MlIntakeForm>();
        public DbSet<PatientMessageRequest> PatientMessageRequests => Set<PatientMessageRequest>();
        public DbSet<DoctorAvailability> DoctorAvailabilities => Set<DoctorAvailability>();
        public DbSet<Prescription> Prescriptions => Set<Prescription>();
        public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<UserActivityLog> UserActivityLogs => Set<UserActivityLog>();
        public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<ApplicationUser>().HasQueryFilter(u => !u.IsSoftDeleted);

            b.Entity<ApplicationUser>()
     .HasOne(u => u.PatientProfile)
     .WithOne(p => p.User)
     .HasForeignKey<PatientProfile>(p => p.UserId)
     .OnDelete(DeleteBehavior.Restrict)
     .IsRequired(false);

            b.Entity<ApplicationUser>()
                .HasOne(u => u.DoctorProfile)
                .WithOne(d => d.User)
                .HasForeignKey<DoctorProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            b.Entity<PatientProfile>().HasIndex(p => p.NationalId);
            b.Entity<DoctorProfile>().HasIndex(d => d.LicenseNumber);
            b.Entity<SystemSetting>().HasIndex(s => s.ClinicName);

            b.Entity<Appointment>()
                .Property(a => a.Status)
                .HasConversion<int>()
                .HasDefaultValue(AppointmentStatus.Pending);

            b.Entity<MedicalRecord>()
                .Property(m => m.Status)
                .HasConversion<int>()
                .HasDefaultValue(RecordStatus.Draft);

            b.Entity<MedicalAttachment>()
                .Property(a => a.Status)
                .HasConversion<int>()
                .HasDefaultValue(AttachmentStatus.Pending);

            b.Entity<LabResult>()
                .Property(l => l.Status)
                .HasConversion<int>()
                .HasDefaultValue(LabResultStatus.Pending);

            b.Entity<AuditLog>()
                .Property(a => a.EventType)
                .HasConversion<int>();

            b.Entity<UserNotification>()
                .Property(n => n.Type)
                .HasConversion<int>();

            b.Entity<MedicalRecord>()
                .HasOne(m => m.Patient)
                .WithMany(p => p.MedicalRecords)
                .HasForeignKey(m => m.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<MedicalRecord>()
                .HasOne(m => m.Doctor)
                .WithMany(d => d.MedicalRecords)
                .HasForeignKey(m => m.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<LabResult>()
                .HasOne(l => l.Patient)
                .WithMany(p => p.LabResults)
                .HasForeignKey(l => l.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<LabResult>()
                .HasOne(l => l.UploadedByAssistant)
                .WithMany()
                .HasForeignKey(l => l.UploadedByAssistantId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            b.Entity<LabResult>()
                .HasOne(l => l.ValidatedByDoctor)
                .WithMany()
                .HasForeignKey(l => l.ValidatedByDoctorId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            b.Entity<MedicalAttachment>()
                .HasOne(a => a.Patient)
                .WithMany(p => p.Attachments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<MedicalAttachment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Attachments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<MedicalAttachment>()
                .HasOne(a => a.ValidatedByDoctor)
                .WithMany()
                .HasForeignKey(a => a.ValidatedByDoctorId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            b.Entity<MedicalAttachment>()
                .HasOne(a => a.UploadedByAssistant)
                .WithMany()
                .HasForeignKey(a => a.UploadedByAssistantId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            
            b.Entity<InternalMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);   

            b.Entity<InternalMessage>()
                .HasOne(m => m.Recipient)
                .WithMany()
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);   

            b.Entity<Prediction>()
                .HasOne(p => p.Patient)
                .WithMany(pp => pp.Predictions)
                .HasForeignKey(p => p.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<Prediction>()
                .HasOne(p => p.Doctor)
                .WithMany(d => d.Predictions)
                .HasForeignKey(p => p.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<Prediction>()
                .HasOne(p => p.RequestedByAssistant)
                .WithMany()
                .HasForeignKey(p => p.RequestedByAssistantId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            b.Entity<PatientMessageRequest>()
                .HasOne(r => r.Patient)
                .WithMany()
                .HasForeignKey(r => r.PatientId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            b.Entity<PatientMessageRequest>()
                .HasOne(r => r.Doctor)
                .WithMany()
                .HasForeignKey(r => r.DoctorId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            b.Entity<PatientMessageRequest>()
                .HasOne(r => r.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(r => r.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            b.Entity<DoctorAvailability>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Availabilities)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<DoctorAvailability>()
                .HasIndex(a => new { a.DoctorId, a.DayOfWeek })
                .IsUnique();

            
            b.Entity<Prescription>()
                .HasOne(p => p.Patient)
                .WithMany(pat => pat.Prescriptions)
                .HasForeignKey(p => p.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<Prescription>()
                .HasOne(p => p.Doctor)
                .WithMany(doc => doc.Prescriptions)
                .HasForeignKey(p => p.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<Prescription>()
                .HasOne(p => p.MedicalRecord)
                .WithMany()
                .HasForeignKey(p => p.MedicalRecordId)
                .OnDelete(DeleteBehavior.SetNull);

            b.Entity<PrescriptionItem>()
                .HasOne(i => i.Prescription)
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.PrescriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            b.Entity<UserActivityLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            b.Entity<UserNotification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);  

            b.Entity<MlIntakeForm>(entity =>
            {
                entity.Property(e => e.OxygenSaturation).HasPrecision(5, 2);
                entity.Property(e => e.Temperature).HasPrecision(5, 2);

                entity.HasOne(e => e.CreatedBy)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedById)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);

                entity.HasOne(e => e.Appointment)
                      .WithMany()
                      .HasForeignKey(e => e.AppointmentId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.HasOne(e => e.Patient)
                      .WithMany()
                      .HasForeignKey(e => e.PatientId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
