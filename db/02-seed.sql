
INSERT INTO categories (name, slug, parent_id) VALUES
('Technology', 'tech', NULL),
('Business', 'business', NULL),
('Programming', 'programming', 1), -- Child of Tech
('Data Science', 'data-science', 1), -- Child of Tech
('Leadership', 'leadership', 2),    -- Child of Business
('Time management','time-management',2);

INSERT INTO users (username, email, hashed_password, status, role) VALUES
('haznooo', 'haznooo@gmail.com', '$2a$11$Wzix1n5pdv.9a6qvY4USqu7ro1sFVtD9cO8soRForwcc0hw/Qqk8q', 'active', 'admin');

-- the password of the admin is : 12345
