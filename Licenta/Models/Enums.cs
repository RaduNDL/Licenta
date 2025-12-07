namespace Licenta.Models
{
    public enum AppointmentStatus { Pending = 0, Approved = 1, Completed = 2, Cancelled = 3, Rescheduled = 4, NoShow = 5 }
    public enum RecordStatus { Draft = 0, Final = 1, Validated = 2, Rejected = 3 }
    public enum AttachmentStatus { Pending = 0, Validated = 1, Rejected = 2 }
    public enum LabResultStatus { Pending = 0, Validated = 1, Rejected = 2 }
    public enum AuditEventType { Login = 0, Logout = 1, Create = 2, Read = 3, Update = 4, Delete = 5 }
    public enum NotificationType { Info = 0, Appointment = 1, Document = 2, Prescription = 3, Message = 4, MLResult = 5 }
}
