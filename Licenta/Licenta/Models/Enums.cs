namespace Licenta.Models
{
    public enum AppointmentStatus
    {
        Pending = 0,
        Approved = 1,
        Completed = 2,
        Cancelled = 3,
        Rescheduled = 4,
        NoShow = 5,
        Confirmed = 6,
        Rejected = 7
    }

    public enum AppointmentRescheduleStatus
    {
        Requested = 0,
        Proposed = 1,
        PatientSelected = 2,
        Approved = 3,
        Rejected = 4,
        Cancelled = 5,
        Expired = 6
    }

    public enum RecordStatus
    {
        Draft = 0,
        Final = 1,
        Validated = 2,
        Rejected = 3
    }

    public enum AttachmentStatus
    {
        Pending = 0,
        Validated = 1,
        Rejected = 2
    }

    public enum LabResultStatus
    {
        Pending = 0,
        Validated = 1,
        Rejected = 2
    }

    public enum AuditEventType
    {
        Login = 0,
        Logout = 1,
        Create = 2,
        Read = 3,
        Update = 4,
        Delete = 5
    }

    public enum NotificationType
    {
        General = 0,
        Message = 1,
        Appointment = 2,
        Document = 3,
        Prediction = 4,
        Info = 5,
        System = 6
    }

    public enum VisitStage
    {
        NotArrived = 0,
        CheckedIn = 1,
        InTriage = 2,
        WaitingDoctor = 3,
        InConsultation = 4,
        Finished = 5
    }
}