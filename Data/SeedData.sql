-- =============================================
-- SOMIOD Database Seed Data
-- Purpose: Insert test data for development and testing
-- =============================================

-- Clear existing data (in reverse order due to foreign key constraints)
DELETE FROM Subscriptions;
DELETE FROM ContentInstances;
DELETE FROM Containers;
DELETE FROM Applications;

-- Reset identity seeds
DBCC CHECKIDENT ('Subscriptions', RESEED, 0);
DBCC CHECKIDENT ('ContentInstances', RESEED, 0);
DBCC CHECKIDENT ('Containers', RESEED, 0);
DBCC CHECKIDENT ('Applications', RESEED, 0);

-- =============================================
-- Insert Applications
-- =============================================
INSERT INTO Applications (Name, CreationDateTime) VALUES
('smart-home', '2024-01-15T10:30:00'),
('weather-station', '2024-01-16T14:20:00'),
('inventory-management', '2024-01-17T09:15:00'),
('fleet-tracking', '2024-01-18T11:45:00'),
('energy-monitoring', '2024-01-19T16:00:00');

-- =============================================
-- Insert Containers (linked to Applications)
-- =============================================

-- Containers for 'smart-home' (ID=1)
INSERT INTO Containers (Name, ParentId, CreationDateTime) VALUES
('living-room', 1, '2024-01-15T10:35:00'),
('bedroom', 1, '2024-01-15T10:40:00'),
('kitchen', 1, '2024-01-15T10:45:00'),
('garage', 1, '2024-01-15T10:50:00');

-- Containers for 'weather-station' (ID=2)
INSERT INTO Containers (Name, ParentId, CreationDateTime) VALUES
('temperature-sensors', 2, '2024-01-16T14:25:00'),
('humidity-sensors', 2, '2024-01-16T14:30:00'),
('wind-sensors', 2, '2024-01-16T14:35:00');

-- Containers for 'inventory-management' (ID=3)
INSERT INTO Containers (Name, ParentId, CreationDateTime) VALUES
('warehouse-a', 3, '2024-01-17T09:20:00'),
('warehouse-b', 3, '2024-01-17T09:25:00'),
('retail-store', 3, '2024-01-17T09:30:00');

-- Containers for 'fleet-tracking' (ID=4)
INSERT INTO Containers (Name, ParentId, CreationDateTime) VALUES
('trucks', 4, '2024-01-18T11:50:00'),
('vans', 4, '2024-01-18T11:55:00');

-- Containers for 'energy-monitoring' (ID=5)
INSERT INTO Containers (Name, ParentId, CreationDateTime) VALUES
('solar-panels', 5, '2024-01-19T16:05:00'),
('battery-storage', 5, '2024-01-19T16:10:00');

-- =============================================
-- Insert Content Instances (linked to Containers)
-- =============================================

-- Content for 'living-room' container (ID=1)
INSERT INTO ContentInstances (Name, ContentType, Content, ParentId, CreationDateTime) VALUES
('temp-reading-001', 'application/json', '{"temperature": 22.5, "unit": "celsius", "timestamp": "2024-01-20T10:00:00"}', 1, '2024-01-20T10:00:00'),
('temp-reading-002', 'application/json', '{"temperature": 23.0, "unit": "celsius", "timestamp": "2024-01-20T10:15:00"}', 1, '2024-01-20T10:15:00'),
('light-status', 'application/json', '{"status": "on", "brightness": 75, "color": "warm-white"}', 1, '2024-01-20T10:30:00');

-- Content for 'bedroom' container (ID=2)
INSERT INTO ContentInstances (Name, ContentType, Content, ParentId, CreationDateTime) VALUES
('temp-reading-001', 'application/json', '{"temperature": 20.0, "unit": "celsius", "timestamp": "2024-01-20T10:00:00"}', 2, '2024-01-20T10:00:00'),
('humidity-reading', 'application/json', '{"humidity": 45, "unit": "percent"}', 2, '2024-01-20T10:05:00');

-- Content for 'kitchen' container (ID=3)
INSERT INTO ContentInstances (Name, ContentType, Content, ParentId, CreationDateTime) VALUES
('fridge-temp', 'application/json', '{"temperature": 4.5, "unit": "celsius", "door": "closed"}', 3, '2024-01-20T10:00:00'),
('oven-status', 'application/json', '{"status": "off", "temperature": 0}', 3, '2024-01-20T10:05:00');

-- Content for 'temperature-sensors' container (ID=5)
INSERT INTO ContentInstances (Name, ContentType, Content, ParentId, CreationDateTime) VALUES
('outdoor-temp-001', 'application/json', '{"temperature": 15.2, "location": "north", "timestamp": "2024-01-20T11:00:00"}', 5, '2024-01-20T11:00:00'),
('outdoor-temp-002', 'application/json', '{"temperature": 15.8, "location": "north", "timestamp": "2024-01-20T11:15:00"}', 5, '2024-01-20T11:15:00'),
('outdoor-temp-003', 'application/json', '{"temperature": 16.1, "location": "north", "timestamp": "2024-01-20T11:30:00"}', 5, '2024-01-20T11:30:00');

-- Content for 'humidity-sensors' container (ID=6)
INSERT INTO ContentInstances (Name, ContentType, Content, ParentId, CreationDateTime) VALUES
('humidity-001', 'application/json', '{"humidity": 68, "location": "outdoor", "timestamp": "2024-01-20T11:00:00"}', 6, '2024-01-20T11:00:00'),
('humidity-002', 'application/json', '{"humidity": 65, "location": "outdoor", "timestamp": "2024-01-20T11:30:00"}', 6, '2024-01-20T11:30:00');

-- Content for 'warehouse-a' container (ID=8)
INSERT INTO ContentInstances (Name, ContentType, Content, ParentId, CreationDateTime) VALUES
('inventory-count', 'application/json', '{"item": "widget-a", "quantity": 1500, "location": "shelf-12"}', 8, '2024-01-20T09:00:00'),
('low-stock-alert', 'text/plain', 'Widget-B stock below threshold: 45 units remaining', 8, '2024-01-20T09:15:00');

-- Content for 'trucks' container (ID=11)
INSERT INTO ContentInstances (Name, ContentType, Content, ParentId, CreationDateTime) VALUES
('truck-01-location', 'application/json', '{"vehicle": "truck-01", "lat": 38.7223, "lon": -9.1393, "speed": 65, "timestamp": "2024-01-20T12:00:00"}', 11, '2024-01-20T12:00:00'),
('truck-02-location', 'application/json', '{"vehicle": "truck-02", "lat": 38.7500, "lon": -9.1500, "speed": 55, "timestamp": "2024-01-20T12:00:00"}', 11, '2024-01-20T12:00:00');

-- Content for 'solar-panels' container (ID=13)
INSERT INTO ContentInstances (Name, ContentType, Content, ParentId, CreationDateTime) VALUES
('power-generation', 'application/json', '{"power": 4.5, "unit": "kW", "efficiency": 92, "timestamp": "2024-01-20T13:00:00"}', 13, '2024-01-20T13:00:00'),
('panel-status', 'application/json', '{"status": "operational", "temperature": 35.2, "voltage": 48}', 13, '2024-01-20T13:05:00');

-- =============================================
-- Insert Subscriptions (linked to Containers)
-- Event types: 1 = creation, 2 = deletion, 3 = both
-- =============================================

-- Subscriptions for 'living-room' container (ID=1)
INSERT INTO Subscriptions (Name, Evt, Endpoint, ParentId, CreationDateTime) VALUES
('temp-alert-service', 1, 'http://localhost:8080/alerts/temperature', 1, '2024-01-20T08:00:00'),
('logging-service', 3, 'http://localhost:8080/logs/living-room', 1, '2024-01-20T08:05:00');

-- Subscriptions for 'bedroom' container (ID=2)
INSERT INTO Subscriptions (Name, Evt, Endpoint, ParentId, CreationDateTime) VALUES
('hvac-control', 1, 'http://localhost:8080/hvac/bedroom', 2, '2024-01-20T08:10:00');

-- Subscriptions for 'temperature-sensors' container (ID=5)
INSERT INTO Subscriptions (Name, Evt, Endpoint, ParentId, CreationDateTime) VALUES
('weather-api-sync', 1, 'http://api.weather.com/v1/ingest', 5, '2024-01-20T08:15:00'),
('data-archiver', 3, 'http://localhost:8080/archive/weather', 5, '2024-01-20T08:20:00');

-- Subscriptions for 'humidity-sensors' container (ID=6)
INSERT INTO Subscriptions (Name, Evt, Endpoint, ParentId, CreationDateTime) VALUES
('humidity-logger', 1, 'http://localhost:8080/logs/humidity', 6, '2024-01-20T08:25:00');

-- Subscriptions for 'warehouse-a' container (ID=8)
INSERT INTO Subscriptions (Name, Evt, Endpoint, ParentId, CreationDateTime) VALUES
('stock-notification', 1, 'http://localhost:8080/notifications/stock', 8, '2024-01-20T08:30:00'),
('erp-sync', 3, 'http://erp.company.com/api/inventory', 8, '2024-01-20T08:35:00');

-- Subscriptions for 'trucks' container (ID=11)
INSERT INTO Subscriptions (Name, Evt, Endpoint, ParentId, CreationDateTime) VALUES
('fleet-dashboard', 1, 'http://localhost:8080/fleet/updates', 11, '2024-01-20T08:40:00'),
('route-optimizer', 1, 'http://localhost:8080/routing/optimize', 11, '2024-01-20T08:45:00');

-- Subscriptions for 'solar-panels' container (ID=13)
INSERT INTO Subscriptions (Name, Evt, Endpoint, ParentId, CreationDateTime) VALUES
('energy-dashboard', 1, 'http://localhost:8080/energy/dashboard', 13, '2024-01-20T08:50:00'),
('grid-sync', 1, 'http://localhost:8080/grid/sync', 13, '2024-01-20T08:55:00'),
('maintenance-alerts', 2, 'http://localhost:8080/maintenance/solar', 13, '2024-01-20T09:00:00');

-- =============================================
-- Verification Queries (commented out - uncomment to verify)
-- =============================================

-- SELECT 'Applications' as TableName, COUNT(*) as RecordCount FROM Applications
-- UNION ALL
-- SELECT 'Containers', COUNT(*) FROM Containers
-- UNION ALL
-- SELECT 'ContentInstances', COUNT(*) FROM ContentInstances
-- UNION ALL
-- SELECT 'Subscriptions', COUNT(*) FROM Subscriptions;

-- -- Show application hierarchy
-- SELECT 
--     a.Name as Application,
--     c.Name as Container,
--     ci.Name as ContentInstance,
--     s.Name as Subscription
-- FROM Applications a
-- LEFT JOIN Containers c ON c.ParentId = a.Id
-- LEFT JOIN ContentInstances ci ON ci.ParentId = c.Id
-- LEFT JOIN Subscriptions s ON s.ParentId = c.Id
-- ORDER BY a.Id, c.Id, ci.Id, s.Id;

PRINT 'Seed data inserted successfully!';
PRINT '- 5 Applications';
PRINT '- 14 Containers';
PRINT '- 18 Content Instances';
PRINT '- 13 Subscriptions';
