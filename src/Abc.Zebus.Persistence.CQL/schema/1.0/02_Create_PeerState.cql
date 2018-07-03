CREATE TABLE "PeerState" (
	"PeerId" ascii,
	"NonAckedMessageCount" int,
	"OldestNonAckedMessageTimestamp" bigint,
	PRIMARY KEY ("PeerId")
);