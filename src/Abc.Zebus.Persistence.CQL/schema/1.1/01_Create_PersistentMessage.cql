CREATE TABLE "PersistentMessage" (
	"PeerId" ascii,
	"BucketId" bigint,
	"UniqueTimestampInTicks" bigint,
	"MessageId" uuid,
	"IsAcked" boolean,
	"TransportMessage" blob,
	PRIMARY KEY (( "PeerId", "BucketId" ), "UniqueTimestampInTicks", "MessageId")
);