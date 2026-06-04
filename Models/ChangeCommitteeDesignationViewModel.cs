using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class ChangeCommitteeDesignationViewModel
    {
        public int ResidentId { get; set; }

        public string ResidentName { get; set; } = string.Empty;

        public string FlatNumber { get; set; } = string.Empty;

        public bool HasLogin { get; set; }

        public bool IsCommitteeMember { get; set; }

        [Display(Name = "Current role")]
        public string CurrentRoleLabel { get; set; } = string.Empty;

        [Display(Name = "New committee role")]
        public string? NewDesignation { get; set; }

        [Display(Name = "Remove committee role (back to Owner — keeps login)")]
        public bool RemoveCommitteeRole { get; set; }

        public List<string> AvailableDesignations { get; set; } = new();

        public List<DesignationAssignmentRow> CurrentAssignments { get; set; } = new();
    }

    public class DesignationAssignmentRow
    {
        public string Designation { get; set; } = string.Empty;

        public string? AssignedTo { get; set; }

        public bool IsVacant { get; set; }
    }
}
