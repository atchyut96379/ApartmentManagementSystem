using System;
using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class Maintenance
    {
        public int Id { get; set; }

        [Required]
        public string FlatNumber { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public string Month { get; set; }

        [Required]
        public int Year { get; set; }

        public DateTime DueDate { get; set; }

        // IMPORTANT FIX
        public bool PaymentStatus { get; set; }

        public DateTime? PaymentDate { get; set; }

        public string Remarks { get; set; }

        public string? ReceiptNumber { get; set; }

        public DateTime? PaidDate { get; set; }

        public decimal FineAmount { get; set; }

        public decimal TotalPaidAmount { get; set; }

        public string? TransactionId { get; set; }

        public string? PayerName { get; set; }

        public string? PaymentGateway { get; set; }

        public string? RazorpayOrderId { get; set; }
    }
}