# ServiceHub

ServiceHub is a full-stack appointment and service-booking platform built with **React, ASP.NET Core and PostgreSQL**.

It allows businesses to manage their services, working hours and temporary closures while allowing customers to discover businesses, view real-time availability and book appointments.

## Live Demo

**Web application:**  
https://service-hub-gray-omega.vercel.app

**API health:**  
https://servicehub-api-prod-ahajfycdh9cuhscp.australiaeast-01.azurewebsites.net/api/health

## Features

### Customers
- Register and log in securely
- Browse active businesses
- View business profiles and available services
- Select a date and view available appointment times
- Create appointments
- View upcoming and previous bookings
- Cancel upcoming confirmed bookings

### Business Owners
- Create and edit a business profile
- Manage services, prices and durations
- Configure weekly working hours
- Add temporary closures with custom start and end dates/times
- View customer bookings
- Cancel confirmed bookings
- Mark completed appointments

### Availability and Booking
- Availability calculated using each business's configured Australian time zone
- Business-local times converted to UTC on the backend
- 15-minute appointment boundaries
- Temporary closures automatically remove overlapping slots
- Existing confirmed bookings block conflicting appointments
- Availability is revalidated before every booking
- PostgreSQL provides final protection against concurrent overlapping bookings
- Concurrent booking conflicts safely return HTTP `409 Conflict`

## Tech Stack

### Frontend
- React
- TypeScript
- Vite
- Bootstrap
- React Router

### Backend
- C#
- ASP.NET Core (.NET 10)
- ASP.NET Core Identity
- JWT authentication
- Entity Framework Core

### Database
- PostgreSQL
- PostgreSQL `btree_gist` exclusion constraints

### Deployment
- **Frontend:** Vercel
- **Backend:** Azure App Service
- **Database:** Azure Database for PostgreSQL Flexible Server

## Architecture

```text
React + TypeScript + Vite
          |
       REST / JSON
          |
ASP.NET Core Web API
          |
Application Services
          |
ASP.NET Core Identity + EF Core
          |
      PostgreSQL
