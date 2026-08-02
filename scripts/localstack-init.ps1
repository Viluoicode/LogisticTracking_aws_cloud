# Bootstrap SNS + SQS trên LocalStack cho dev local.
# Tạo: topic shipment-status-changed, 2 queue (tracking/notif), subscribe fan-out (raw delivery).
# In ra TopicArn ở dòng cuối để set env SNS_TOPIC_ARN.
param(
    [string]$Endpoint = "http://localhost:4566",
    [string]$Region   = "ap-southeast-1"
)

function awsls { aws --endpoint-url $Endpoint --region $Region @args }

$topicArn    = awsls sns create-topic --name shipment-status-changed --query TopicArn --output text
$trackingUrl = awsls sqs create-queue --queue-name tracking-queue --query QueueUrl --output text
$notifUrl    = awsls sqs create-queue --queue-name notif-queue --query QueueUrl --output text
$trackingArn = awsls sqs get-queue-attributes --queue-url $trackingUrl --attribute-names QueueArn --query "Attributes.QueueArn" --output text
$notifArn    = awsls sqs get-queue-attributes --queue-url $notifUrl    --attribute-names QueueArn --query "Attributes.QueueArn" --output text

awsls sns subscribe --topic-arn $topicArn --protocol sqs --notification-endpoint $trackingArn --attributes RawMessageDelivery=true | Out-Null
awsls sns subscribe --topic-arn $topicArn --protocol sqs --notification-endpoint $notifArn    --attributes RawMessageDelivery=true | Out-Null

Write-Output $topicArn
