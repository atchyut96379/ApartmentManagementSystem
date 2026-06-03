using System;
using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [Required]
        public string ExpenseType { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime ExpenseDate { get; set; }

        public string Description { get; set; }
    }
}