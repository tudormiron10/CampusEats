namespace CampusEats.Api.Infrastructure.Persistence.Entities;

//
public enum OrderStatus { Pending, InPreparation, Ready, Completed, Cancelled }
//
public enum PaymentStatus { Initiated, Successful, Failed }

public enum UserRole { Admin, Client, Kitchen}
