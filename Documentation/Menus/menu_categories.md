Menu Categories API 

Actions:
Create a new category
Edit an existing category
Delete a category
Attach category to a menu (already handled via menu_id in create)

Database Schema:
CREATE TABLE menu_categories (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    menu_id UUID NOT NULL REFERENCES menus(id) ON DELETE CASCADE,
    organization_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    location_id UUID REFERENCES locations(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    sort_order INT DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

API endpoints: 

POST /api/menu-categories - Create category
PUT /api/menu-categories/{id} - Update category
DELETE /api/menu-categories/{id} - Delete category
GET /api/menu-categories/{id} - Get category by ID
GET /api/menus/{menuId}/categories - Get all categories for a menu
GET	/api/organizations/{organizationId}/menu-categories	Get all categories for an organization
GET	/api/locations/{locationId}/menu-categories	Get all categories for a location
