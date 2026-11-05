# Bài tập Redis — tự kiểm tra và thực hành

> **Cách làm:** mở `redis-cli`, gõ từng lệnh, quan sát kết quả rồi **tự trả lời câu hỏi trước**. Sau đó mới mở phần **"Xem đáp án"** để so sánh. Mỗi bài ghi rõ mục tương ứng trong `Knowledge.md` để đọc lại khi cần.

Các bài tập dùng `redis-cli` kết nối Redis local. Nếu CLI của bạn cần chỉ định host/port, thêm `-h HOST -p PORT` vào lệnh, ví dụ `redis-cli -h 127.0.0.1 -p 6379 PING`.

**An toàn:** chỉ chạy các lệnh ghi trong môi trường local/dev. Bài tập dùng tiền tố `learn:` để dễ phân biệt. Không dùng `FLUSHDB`, `FLUSHALL` hoặc `KEYS *` trên production. Dọn đúng các key đã tạo bằng `DEL` ở cuối.

## Bài 1 — String, TTL và cache miss

📖 Ôn lại: `Knowledge.md` mục 2, 3, 4.1

Chạy:

```text
SET learn:cache "Redis basics" EX 120
GET learn:cache
TTL learn:cache
```

Sau đó xóa key rồi kiểm tra lại:

```text
DEL learn:cache
TTL learn:cache
GET learn:cache
```

**Tự trả lời:** TTL trả về gì khi key chưa tồn tại? Cache miss nên được ứng dụng xử lý thế nào? Vì sao TTL hữu ích nhưng không phải key nào cũng bắt buộc có TTL?

**Gợi ý:** So sánh `TTL` trước và sau `DEL`. Khi cache miss, ứng dụng thường đọc nguồn dữ liệu chính, rồi có thể ghi kết quả trở lại cache.

<details>
<summary>Xem đáp án Bài 1</summary>

- Sau `DEL`, `TTL learn:cache` trả `(integer) -2` = key **không tồn tại**. `GET` trả `(nil)`.
- Trước khi xóa, `TTL` trả số giây còn lại (khoảng 120 trở xuống).
- **Cache miss:** ứng dụng đọc database → `SET` lại vào Redis kèm TTL → trả kết quả cho người dùng (quy trình cache-aside).
- **Không phải key nào cũng cần TTL:** dữ liệu tạm (cache, OTP, session) nên có TTL. Dữ liệu lâu dài (ví dụ bảng xếp hạng cả mùa giải) thì không đặt TTL, nhưng phải có kế hoạch dọn dẹp.

</details>

## Bài 2 — Chọn đúng data type

📖 Ôn lại: `Knowledge.md` mục 4

Thử tạo một user Hash, danh sách gần đây, Set tag và Sorted Set leaderboard:

```text
HSET learn:user:1 name Ada role learner
HGETALL learn:user:1
LPUSH learn:recent "lesson-2" "lesson-1"
LRANGE learn:recent 0 9
SADD learn:tags redis database redis
SMEMBERS learn:tags
ZADD learn:leaderboard 120 Ada 95 Lin
ZREVRANGE learn:leaderboard 0 9 WITHSCORES
```

**Tự trả lời:** Vì sao thêm `redis` hai lần vào Set nhưng chỉ thấy một phần tử? Kiểu nào hợp với bảng xếp hạng? Nếu cần lấy user theo từng field thì vì sao Hash phù hợp?

**Gợi ý:** Set giữ thành viên duy nhất; Sorted Set có score để sắp thứ tự; Hash gom nhiều field dưới một key.

<details>
<summary>Xem đáp án Bài 2</summary>

- `SADD learn:tags redis database redis` trả `(integer) 2` vì **Set không cho trùng**. Lần thêm `redis` thứ hai bị bỏ qua.
- Bảng xếp hạng → **Sorted Set**, vì mỗi member có score và Redis tự sắp xếp.
- Hash phù hợp vì có thể đọc/sửa **từng field** (`HGET learn:user:1 name`) mà không phải đọc/ghi cả object như khi lưu JSON trong String.
- `LRANGE learn:recent 0 9` trả `lesson-1` trước `lesson-2`, vì `LPUSH` thêm từng phần tử vào **đầu** danh sách: `lesson-2` vào trước, rồi `lesson-1` chèn lên trước nó.

</details>

## Bài 3 — Duyệt key an toàn

📖 Ôn lại: `Knowledge.md` mục 6 và mục 10 (Lỗi 1)

Tạo vài key thử nghiệm và quét bằng cursor:

```text
SET learn:scan:1 one
SET learn:scan:2 two
SCAN 0 MATCH learn:scan:* COUNT 10
```

Lệnh trả về cursor và danh sách key. Nếu cursor chưa phải `0`, dùng cursor đó làm đối số đầu tiên của lần gọi `SCAN` tiếp theo cho đến khi cursor quay về `0`.

**Tự trả lời:** Vì sao `SCAN` phù hợp hơn `KEYS *` trên production? Có đảm bảo `SCAN` trả mỗi key đúng một lần hoặc tạo snapshot nhất quán không?

**Gợi ý:** `COUNT` là gợi ý số lượng, không phải giới hạn cứng. Kết quả có thể trùng; vòng lặp quét nhiều lần có thể cần đi qua cả keyspace. Thiết kế consumer idempotent.

<details>
<summary>Xem đáp án Bài 3</summary>

- `KEYS *` quét **toàn bộ** key trong một lệnh. Vì Redis chạy lệnh trên một luồng chính, mọi request khác phải chờ. `SCAN` chia việc quét thành nhiều đợt nhỏ, giữa các đợt Redis vẫn phục vụ request khác.
- **Không đảm bảo:** `SCAN` có thể trả một key nhiều lần. Key được thêm hoặc xóa trong lúc quét có thể xuất hiện hoặc không. Chỉ dừng khi cursor trả về `"0"`.
- `COUNT 10` chỉ là **gợi ý** khối lượng việc mỗi đợt, không đảm bảo trả đúng 10 key.

</details>

## Bài 4 — Pub/Sub so với Streams

📖 Ôn lại: `Knowledge.md` mục 5.2, 5.3

Mở hai terminal. Ở terminal A:

```text
SUBSCRIBE learn:notifications
```

Ở terminal B:

```text
PUBLISH learn:notifications "hello"
```

Thử publish khi terminal A đã unsubscribe hoặc chưa subscribe.

**Tự trả lời:** Subscriber offline có nhận lại message không? Với công việc cần lưu để đọc lại, acknowledgement và consumer group thì nên xem xét tính năng nào?

**Gợi ý:** Pub/Sub là phát trực tiếp, không lưu message. Tìm hiểu Streams, `XGROUP`, `XREADGROUP`, `XACK` và pending entries list.

<details>
<summary>Xem đáp án Bài 4</summary>

- Subscriber offline **không nhận lại** message. Pub/Sub giống loa phát thanh, không có bản ghi âm.
- `PUBLISH` trả về số subscriber đang nghe. Khi không ai subscribe, nó trả `(integer) 0` và message bị mất.
- Cần lưu lại, ACK, consumer group → dùng **Stream**.

</details>

## Bài 5 — Stream entry và acknowledgement

📖 Ôn lại: `Knowledge.md` mục 5.3

Chạy các lệnh sau trên local:

```text
XADD learn:events * event lesson-completed user Ada
XGROUP CREATE learn:events learners 0 MKSTREAM
XREADGROUP GROUP learners worker-1 COUNT 10 STREAMS learn:events >
XPENDING learn:events learners
```

Lấy ID được trả về từ `XREADGROUP`, thay `<message-id>` trong lệnh:

```text
XACK learn:events learners <message-id>
XPENDING learn:events learners
```

**Tự trả lời:** Pending có ý nghĩa gì? Điều gì xảy ra nếu consumer chết sau khi đọc nhưng trước khi `XACK`? Vì sao retry vẫn cần idempotency?

**Gợi ý:** Đọc nhưng chưa ACK thường còn trong pending list để có thể được kiểm tra và xử lý lại. ACK không tự rollback hay đảm bảo nghiệp vụ chỉ chạy một lần.

<details>
<summary>Xem đáp án Bài 5</summary>

- **Pending** = message đã giao cho một consumer nhưng **chưa được ACK**. Sau `XREADGROUP`, `XPENDING` cho thấy 1 message pending của `worker-1`. Sau `XACK`, số pending về 0.
- Consumer chết trước khi `XACK` → message **vẫn nằm trong pending**. Consumer khác có thể nhận lại bằng `XCLAIM` hoặc `XAUTOCLAIM`.
- Vì message có thể được giao lại, nó có thể bị **xử lý 2 lần** (ví dụ đã trừ tiền nhưng chưa kịp ACK). Code xử lý phải **idempotent**: kiểm tra "đơn này đã xử lý chưa?" trước khi làm.

</details>

## Bài 6 — TTL, eviction và bộ nhớ

📖 Ôn lại: `Knowledge.md` mục 9

Xem thông tin hiện tại:

```text
INFO memory
INFO stats
CONFIG GET maxmemory
CONFIG GET maxmemory-policy
```

**Tự trả lời:** `maxmemory` bằng 0 có nghĩa gì trong cấu hình mặc định? Eviction khác expiration thế nào? Nếu Redis là cache, vì sao cần chọn eviction policy và theo dõi key bị evict?

**Gợi ý:** TTL là vòng đời một key; eviction là hành động loại key khi bộ nhớ chạm giới hạn theo policy. không đổi cấu hình server dùng chung nếu chưa hiểu tác động.

<details>
<summary>Xem đáp án Bài 6</summary>

- `maxmemory 0` = **không giới hạn**. Redis có thể dùng hết RAM máy. Production nên đặt giới hạn.
- **Expiration:** key tự xóa khi hết TTL, do **bạn** quyết định. **Eviction:** Redis tự xóa bớt key khi chạm `maxmemory`, theo **policy**.
- Mặc định `maxmemory-policy` là `noeviction`: đầy RAM thì Redis **báo lỗi khi ghi**. Với cache nên chọn `allkeys-lru` hoặc `allkeys-lfu`.
- Theo dõi `evicted_keys` trong `INFO stats`. Số này tăng nhiều nghĩa là RAM không đủ, cache bị xóa sớm và tỉ lệ miss tăng.

</details>

## Bài 7 — Đọc tình huống thiết kế

📖 Ôn lại: `Knowledge.md` mục 4.7, 5, 11

Với mỗi yêu cầu, chọn Redis data type hoặc cơ chế phù hợp và nêu một rủi ro cần xử lý:

1. Cache response sản phẩm trong 5 phút.
2. Ghi nhận danh sách tag không trùng.
3. Bảng xếp hạng theo điểm.
4. Gửi thông báo trực tiếp cho client đang kết nối; không cần lưu message.
5. Tiếp nhận sự kiện cần consumer group, ACK và xử lý lại khi consumer lỗi.
6. Lưu dữ liệu nghiệp vụ quan trọng cần backup/restore.

**Gợi ý:** Lần lượt cân nhắc String + TTL, Set, Sorted Set, Pub/Sub, Streams và yêu cầu persistence/backup. Không chọn database chỉ dựa vào tốc độ đọc.

<details>
<summary>Xem đáp án Bài 7</summary>

| # | Chọn | Rủi ro cần xử lý |
|---|---|---|
| 1 | String + `EX 300` | Dữ liệu cũ khi giá thay đổi → `DEL` khi cập nhật. Cache stampede khi key hot hết hạn. |
| 2 | Set | Set quá lớn (big key) nếu không giới hạn. |
| 3 | Sorted Set | Đừng lấy cả bảng (`ZRANGE 0 -1`), chỉ lấy top N. |
| 4 | Pub/Sub | Client offline mất message. |
| 5 | Stream + consumer group | Message pending khi consumer lỗi, xử lý trùng → cần idempotent. |
| 6 | Database chính (SQL…), Redis chỉ làm cache. Nếu bắt buộc dùng Redis: bật RDB + AOF, backup ra nơi khác, thử restore. | Mất dữ liệu khi sự cố. Persistence/replica không thay được backup. |

</details>

## Bài 8 — RDB, AOF, replication và Cluster

📖 Ôn lại: `Knowledge.md` mục 7, 8

Không cần thay đổi cấu hình để trả lời:

1. Khi nào snapshot RDB có thể làm mất những ghi gần nhất?
2. AOF ghi lại điều gì và vì sao fsync policy ảnh hưởng durability/hiệu năng?
3. Replica có phải là backup không? Vì sao?
4. Redis Cluster chia dữ liệu ra sao, và vì sao thao tác nhiều key có thể phụ thuộc hash slot?
5. Phân biệt RPO và RTO.

**Gợi ý:** Nêu trade-off và giới hạn, không chỉ ghi tên công nghệ.

<details>
<summary>Xem đáp án Bài 8</summary>

1. RDB chụp ảnh định kỳ. Sự cố xảy ra giữa 2 lần chụp thì **mất mọi thay đổi từ lần chụp cuối**.
2. AOF ghi lại **từng lệnh thay đổi dữ liệu**. `fsync` là lệnh ép hệ điều hành ghi thật xuống đĩa. Ghi mỗi lệnh (`always`) an toàn nhất nhưng chậm. Mỗi giây (`everysec`, mặc định) cân bằng giữa hai yếu tố, có thể mất khoảng 1 giây dữ liệu.
3. Replica **không phải backup**: lỡ xóa dữ liệu trên primary thì lệnh xóa cũng được chép sang replica.
4. Cluster chia keyspace thành **16.384 hash slot** trên nhiều primary. Lệnh nhiều key chỉ chạy khi các key cùng slot. Dùng hash tag `{...}` để ép các key về cùng slot.
5. **RPO** = chấp nhận mất tối đa bao nhiêu dữ liệu. **RTO** = chấp nhận hệ thống ngừng tối đa bao lâu.

</details>

## Bài 9 — Latency và big key

📖 Ôn lại: `Knowledge.md` mục 6 và mục 10 (Lỗi 3)

Không tạo dữ liệu lớn để thử gây tải. Hãy xem thông tin chẩn đoán trên Redis local:

```text
SLOWLOG GET 10
INFO commandstats
```

Sau đó đọc các thao tác sau và xếp hạng theo rủi ro khi danh sách/hash lớn lên:

```text
LRANGE some:list 0 -1
HGETALL some:hash
LRANGE some:list 0 9
HSCAN some:hash 0 COUNT 10
```

**Tự trả lời:** Vì sao một lệnh tốn nhiều thời gian có thể làm tăng latency của request khác? `SCAN`/`HSCAN` có đảm bảo quét xong trong một lệnh hoặc trả một snapshot nhất quán không? Pipeline lớn có luôn tốt hơn không?

**Gợi ý:** Lệnh trả về cả collection có thể tốn CPU, mạng và bộ nhớ phía client. Dùng thao tác có giới hạn hoặc cursor-based scan; chia pipeline thành batch có giới hạn. Không dùng `KEYS *` hay tạo big key trên production để kiểm tra.

<details>
<summary>Xem đáp án Bài 9</summary>

- Rủi ro từ cao đến thấp: `LRANGE some:list 0 -1` và `HGETALL some:hash` (lấy **toàn bộ**, càng lớn càng chậm) > `HSCAN ... COUNT 10` và `LRANGE some:list 0 9` (có giới hạn).
- Redis chạy lệnh trên **một luồng chính**. Lệnh nặng chiếm luồng thì các lệnh khác phải xếp hàng chờ.
- `SCAN`/`HSCAN` **không** xong trong một lệnh và **không** là snapshot nhất quán. Phải lặp đến khi cursor về `"0"`.
- Pipeline lớn **không phải lúc nào cũng tốt**: gửi quá nhiều lệnh một lần sẽ tốn bộ nhớ cả server lẫn client. Nên chia lô (ví dụ 100 – 1.000 lệnh).

</details>

## Kiểm tra cuối — dọn dữ liệu bài tập

Xóa các key thử nghiệm đã tạo:

```text
DEL learn:cache learn:user:1 learn:recent learn:tags learn:leaderboard
DEL learn:scan:1 learn:scan:2 learn:events
```

Pub/Sub channel không lưu key nên không cần xóa. Không dùng `FLUSHDB` để dọn bài tập.
