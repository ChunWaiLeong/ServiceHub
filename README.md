# ServiceHub

ServiceHub is a full-stack service booking platform that allows customers to discover businesses, view available services, and book appointments online.

Business owners can manage their business profile, services, availability, and customer bookings through a dedicated dashboard.

The project is being developed as a portfolio application to demonstrate full-stack software engineering skills including REST API development, authentication and authorization, relational database design, frontend development, testing, CI/CD, and cloud deployment.

## Planned Features

### Customers

- Register and log in
- Browse businesses and services
- View service details and pricing
- View available appointment times
- Book appointments
- View upcoming and previous bookings
- Cancel or reschedule eligible bookings

### Business Owners

- Create and manage a business profile
- Create, update, and deactivate services
- Configure business availability
- View upcoming bookings
- Manage customer appointments
- View basic business statistics

### Administration

- Manage users and businesses
- Moderate business accounts
- Monitor platform activity

## Technology Stack

### Frontend

- React
- TypeScript
- Vite
- Bootstrap

### Backend

- C#
- ASP.NET Core Web API
- .NET 10
- Entity Framework Core

### Database

- PostgreSQL

### Authentication

- ASP.NET Core Identity
- JWT authentication
- Role-based authorization

### Development & Deployment

- Git & GitHub
- Swagger / OpenAPI
- GitHub Actions
- Cloud deployment

## Project Architecture

The application will use a separated frontend and backend architecture.

```text
React + TypeScript
        |
        | REST API
        v
ASP.NET Core Web API
        |
        | Entity Framework Core
        v
    PostgreSQL
```

The backend is responsible for business logic, authentication, authorization, booking validation, and database access.

The frontend communicates with the backend using REST APIs.

## Project Status

🚧 Currently under development.

Initial development will focus on:

1. Project foundation
2. Database configuration
3. Authentication and authorization
4. Business profiles
5. Service management
6. Availability management
7. Appointment booking
8. Customer and business dashboards
9. Automated testing
10. Production deployment

## Future Enhancements

Possible future features include:

- Reviews and ratings
- Email notifications
- Appointment reminders
- Online payments
- Business analytics
- Favourite businesses
- Maps and location search

## Acknowledgements

Parts of the initial backend architecture were inspired by and adapted from the open-source `kmorpex/booking-service` project.

Original project:

https://github.com/kmorpex/booking-service

The original project is distributed under the MIT License.

ServiceHub is being substantially redesigned and extended as an independent portfolio project.