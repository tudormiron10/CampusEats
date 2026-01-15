namespace CampusEats.Api.Infrastructure.Persistence.Entities;


public enum OrderStatus { Pending, InPreparation, Ready, Completed, Cancelled }

public enum PaymentStatus { Pending, Processing, Succeeded, Failed, Cancelled }

public enum UserRole { Admin, Client, Manager }
