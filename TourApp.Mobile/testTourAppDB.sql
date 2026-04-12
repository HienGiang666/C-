-- Xem tất cả địa điểm
SELECT * FROM POIs;

-- Xem thuyết minh kèm tên địa điểm
SELECT a.*, p.Name as POIName 
FROM Audios a
JOIN POIs p ON a.POIId = p.Id;

-- Xem người dùng
SELECT * FROM Users;

-- Xem tour và số lượng địa điểm
SELECT t.Id, t.Name, t.Description, t.Price, t.IsActive,
       COUNT(tp.POIId) as POICount
FROM Tours t
LEFT JOIN TourPOIs tp ON t.Id = tp.TourId
GROUP BY t.Id, t.Name, t.Description, t.Price, t.IsActive;