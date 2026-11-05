- Redis là gì? mục đích dùng nó
- những tính năng chính của redis : Cache - Message broker - Streaming - database → cho ví dụ về nó, hiểu cách 4 tính năng này hoạt động
- Redis chạy đơn luồng nên có thể chặn các request → cách khắc phục?
- các kiểu dữ liệu của redis : string, list, set, hash, sorted set ⇒ các kiểu dữ liệu trên phù hợp để làm những việc gì?
- Cơ chế lưu xuống ổ đĩa của Redis
- cơ chế replication, cơ chế cluster
- Khi nào nên xài và không nên xài redis
- những việc nên tránh khi sử dụng redis
    1. Sử dụng KEY * trên prod → quét hết cả đống dữ liệu dẫn đến chặn request ⇒ sử dụng SCAN để thay thế
    2. không đặt TTL ⇒ gây đầy ram
    3. BIG KEY gây nghẽn mạng ⇒ chia nhỏ hoặc nén để fix
    4. Cấu hình max memory``
    