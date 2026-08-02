# Bootstrap SNS + SQS (+ DLQ) trên LocalStack cho dev local.
# Topic shipment-status-changed; 2 queue tracking/notif + 2 DLQ; subscribe fan-out (raw delivery);
# redrive maxReceiveCount=3, VisibilityTimeout=2s (để demo DLQ nhanh).
# In ra TopicArn ở dòng cuối.
param(
    [string]$Endpoint = "http://localhost:4566",
    [string]$Region   = "ap-southeast-1"
)

function awsls { aws --endpoint-url $Endpoint --region $Region @args }

$topicArn = awsls sns create-topic --name shipment-status-changed --query TopicArn --output text

# DLQs
$trackingDlqUrl = awsls sqs create-queue --queue-name tracking-dlq --query QueueUrl --output text
$notifDlqUrl    = awsls sqs create-queue --queue-name notif-dlq    --query QueueUrl --output text
$trackingDlqArn = awsls sqs get-queue-attributes --queue-url $trackingDlqUrl --attribute-names QueueArn --query "Attributes.QueueArn" --output text
$notifDlqArn    = awsls sqs get-queue-attributes --queue-url $notifDlqUrl    --attribute-names QueueArn --query "Attributes.QueueArn" --output text

# Main queues
$trackingUrl = awsls sqs create-queue --queue-name tracking-queue --query QueueUrl --output text
$notifUrl    = awsls sqs create-queue --queue-name notif-queue    --query QueueUrl --output text

function Set-Redrive($url, $dlqArn) {
    $redrive = @{ deadLetterTargetArn = $dlqArn; maxReceiveCount = "3" } | ConvertTo-Json -Compress
    $attrs   = @{ RedrivePolicy = $redrive; VisibilityTimeout = "2" } | ConvertTo-Json -Compress
    $f = New-TemporaryFile
    $attrs | Out-File -Encoding ascii $f
    awsls sqs set-queue-attributes --queue-url $url --attributes ("file://" + $f.FullName) | Out-Null
    Remove-Item $f -Force
}
Set-Redrive $trackingUrl $trackingDlqArn
Set-Redrive $notifUrl    $notifDlqArn

# Subscribe fan-out (raw delivery)
$trackingArn = awsls sqs get-queue-attributes --queue-url $trackingUrl --attribute-names QueueArn --query "Attributes.QueueArn" --output text
$notifArn    = awsls sqs get-queue-attributes --queue-url $notifUrl    --attribute-names QueueArn --query "Attributes.QueueArn" --output text
awsls sns subscribe --topic-arn $topicArn --protocol sqs --notification-endpoint $trackingArn --attributes RawMessageDelivery=true | Out-Null
awsls sns subscribe --topic-arn $topicArn --protocol sqs --notification-endpoint $notifArn    --attributes RawMessageDelivery=true | Out-Null

Write-Output $topicArn
